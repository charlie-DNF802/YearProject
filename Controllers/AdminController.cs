using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.Models;
using Ward_Management_System.ViewModels;

namespace Ward_Management_System.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public AdminController(UserManager<Users> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        //Takes you to admin welcome page
        public IActionResult Admin()
        {
            var activeRole = User.Claims.FirstOrDefault(c => c.Type == "ActiveRole")?.Value;

            if (activeRole == "Doctor")
            {
                return RedirectToAction("Index", "Doctor");
            }
            else if (activeRole == "Nurse")
            {
                return RedirectToAction("Index", "Nurse");
            }
            else if (activeRole == "WardAdmin")
            {
                return RedirectToAction("Index", "WardAdmin");
            }
            else if (activeRole == "Sister")
            {
                return RedirectToAction("Index", "Nurse");
            }
            else if (activeRole == "User")
            {
                return RedirectToAction("Index", "User");
            }

            // Default → Admin dashboard
            return View("Admin");
        }

        //Add new staff members
        [HttpGet]
        public IActionResult AddStaff()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStaff(AddStaffViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var staffUser = new Users
            {
                FullName = model.Name,
                UserName = model.Email,
                Email = model.Email,
                NormalizedEmail = model.Email.ToUpper(),
                EmailConfirmed = true,
                Age = model.Age,
                Address = model.Address,
                PhoneNumber = model.PhoneNumber,
                IdNumber = model.IdNumber,
                Gender = model.Gender
            };

            var result = await _userManager.CreateAsync(staffUser, model.DefaultPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(staffUser, model.Role);
                TempData["ToastMessage"] = "Successfully Added!";
                TempData["ToastType"] = "success";
                return RedirectToAction("StaffList");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        // View List of staff members
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> StaffList(string role = "", string gender = "", string ageGroup = "", string search = "", int pg = 1)
        {
            // Get all active users
            var activeUsers = await _userManager.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            var staffList = new List<StaffListViewModel>();

            foreach (var user in activeUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Any(r => r != "Admin" && r != "User")) // Include only staff roles
                {
                    staffList.Add(new StaffListViewModel
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Roles = string.Join(", ", roles),
                        Age = user.Age,
                        Address = user.Address,
                        PhoneNumber = user.PhoneNumber,
                        IdNumber = user.IdNumber,
                        Gender = user.Gender,
                        DateAdded = user.DateAdded
                    });
                }
            }

            // Apply server-side filters
            if (!string.IsNullOrWhiteSpace(role))
            {
                staffList = staffList
                    .Where(s => s.Roles.Split(",").Any(r => r.Trim().Equals(role, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(gender))
            {
                staffList = staffList
                    .Where(s => s.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(ageGroup))
            {
                staffList = staffList.Where(s => ageGroup switch
                {
                    "young" => s.Age < 30,
                    "middle" => s.Age >= 30 && s.Age <= 50,
                    "senior" => s.Age > 50,
                    _ => true
                }).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                staffList = staffList
                    .Where(s => s.FullName.ToLower().Contains(search)
                             || s.Email.ToLower().Contains(search)
                             || s.Roles.ToLower().Contains(search)
                             || s.Address.ToLower().Contains(search)
                             || s.PhoneNumber.ToLower().Contains(search)
                             || s.IdNumber.ToLower().Contains(search)
                             || s.Gender.ToLower().Contains(search))
                    .ToList();
            }

            // ===== 📊 STATS =====
            ViewBag.TotalStaff = staffList.Count;
            ViewBag.DoctorCount = staffList.Count(s => s.Roles.Contains("Doctor"));
            ViewBag.NurseCount = staffList.Count(s => s.Roles.Contains("Nurse"));
            ViewBag.SisterCount = staffList.Count(s => s.Roles.Contains("Sister"));
            ViewBag.WardAdminCount = staffList.Count(s => s.Roles.Contains("WardAdmin"));

            // ===== 📈 EMPLOYEE GROWTH DATA FOR CHART =====
            var months = Enumerable.Range(1, 6)
                .Select(i => DateTime.Now.AddMonths(-6 + i))
                .ToList();

            var monthLabels = months.Select(m => m.ToString("MMM")).ToList();

            var doctorMonthly = months.Select(m =>
                staffList.Count(s => s.Roles.Contains("Doctor") &&
                                     s.DateAdded.Month == m.Month &&
                                     s.DateAdded.Year == m.Year)).ToList();

            var nurseMonthly = months.Select(m =>
                staffList.Count(s => s.Roles.Contains("Nurse") &&
                                     s.DateAdded.Month == m.Month &&
                                     s.DateAdded.Year == m.Year)).ToList();

            var sisterMonthly = months.Select(m =>
                staffList.Count(s => s.Roles.Contains("Sister") &&
                                     s.DateAdded.Month == m.Month &&
                                     s.DateAdded.Year == m.Year)).ToList();

            var wardAdminMonthly = months.Select(m =>
                staffList.Count(s => s.Roles.Contains("WardAdmin") &&
                                     s.DateAdded.Month == m.Month &&
                                     s.DateAdded.Year == m.Year)).ToList();

            ViewBag.MonthLabels = monthLabels;
            ViewBag.DoctorMonthly = doctorMonthly;
            ViewBag.NurseMonthly = nurseMonthly;
            ViewBag.SisterMonthly = sisterMonthly;
            ViewBag.WardAdminMonthly = wardAdminMonthly;

            // ===== PAGING =====
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            int recsCount = staffList.Count;
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = staffList.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;

            // Keep filters for UI
            ViewBag.CurrentRole = role;
            ViewBag.CurrentGender = gender;
            ViewBag.CurrentAgeGroup = ageGroup;
            ViewBag.CurrentSearch = search;

            return View(data);
        }



        //Edit employee
        // GET: Admin/EditStaff/id
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> EditStaff(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(r => r != "Admin" && r != "User");

            var model = new EditViewModel
            {
                Id = user.Id,
                Name = user.FullName,
                Age = user.Age,
                Address = user.Address,
                PhoneNumber = user.PhoneNumber,
                IdNumber = user.IdNumber,
                Gender = user.Gender,
                Email = user.Email,
                Role = role,
                DefaultPassword = "" // Admin can not reset defaultpassword unless required to do.
            };

            return View(model);
        }

        // POST: Admin/EditStaff
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaff(EditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return NotFound();

            // Update fields
            user.FullName = model.Name;
            user.Age = model.Age;
            user.Address = model.Address;
            user.PhoneNumber = model.PhoneNumber;
            user.IdNumber = model.IdNumber;
            user.Gender = model.Gender;
            user.Email = model.Email;
            user.UserName = model.Email;

            // Update password if oonly a new one provided
            if (!string.IsNullOrWhiteSpace(model.DefaultPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, model.DefaultPassword);

                if (!resetResult.Succeeded)
                {
                    foreach (var error in resetResult.Errors)
                        ModelState.AddModelError("", error.Description);
                    return View(model);
                }
            }

            // Update role if changed
            var existingRoles = await _userManager.GetRolesAsync(user);
            var staffRoles = existingRoles.Where(r => r != "Admin" && r != "User").ToList();

            if (staffRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, staffRoles);

            if (!string.IsNullOrEmpty(model.Role))
            {
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["ToastMessage"] = "successfully Edited!";
                TempData["ToastType"] = "success";
                return RedirectToAction("StaffList");
            }


            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        //Delete Staff member
        // POST: Soft delete (set IsActive = false)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);

            if (user != null)
            {
                user.IsActive = false; // Soft delete
                _context.Update(user);
                TempData["ToastMessage"] = "successfully Deleted!";
                TempData["ToastType"] = "success";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(StaffList));
        }

        [Authorize(Roles = "Admin")]
        //GET ALL HOSPITALINFO AND THEIR DEPARTMENTS
        public IActionResult HospitalInfo()
        {
            var hospital = _context.HospitalInfo.ToList();
            return View(hospital);
        }
        //View Medication
        public async Task<IActionResult> ViewMedication(int? categoryId, string brand, string storage, string search, int pg = 1)
        {
            ModelState.Remove("Brand");
            ModelState.Remove("Storage");
            ModelState.Remove("Search");

            var stockMedications = _context.StockMedications
                                           .Include(m => m.MedicationCategory)
                                           .Where(m => m.IsActive)
                                           .AsQueryable();

            // Filters
            if (!string.IsNullOrEmpty(brand))
                stockMedications = stockMedications.Where(m => m.Brand == brand);

            if (!string.IsNullOrEmpty(storage))
                stockMedications = stockMedications.Where(m => m.Storage == storage);

            if (categoryId.HasValue)
                stockMedications = stockMedications.Where(m => m.MedicationCategory.ID == categoryId.Value);

            if (!string.IsNullOrEmpty(search))
                stockMedications = stockMedications.Where(m => m.Name.Contains(search));

            // Get all Brands, Storages, Categories for filters
            ViewBag.Categories = await _context.MedicationCategory.ToListAsync();
            ViewBag.Brands = await _context.StockMedications
                                           .Where(m => m.IsActive)
                                           .Select(m => m.Brand)
                                           .Distinct()
                                           .ToListAsync();
            ViewBag.Storages = await _context.StockMedications
                                             .Where(m => m.IsActive)
                                             .Select(m => m.Storage)
                                             .Distinct()
                                             .ToListAsync();

            // Hold current filter values
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentBrand = brand;
            ViewBag.CurrentStorage = storage;
            ViewBag.CurrentSearch = search;

            // Summary counts per category
            var categorySummary = await _context.StockMedications
                .Where(m => m.IsActive)
                .GroupBy(m => m.MedicationCategory.Name)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.CategorySummary = categorySummary;


            // Paging
            const int pageSize = 5;
            if (pg < 1) pg = 1;

            int recsCount = await stockMedications.CountAsync();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = await stockMedications.Skip(recSkip).Take(pageSize).ToListAsync();

            ViewBag.Pager = pager;

            return View(data);
        }


        ////Add new medications
        [HttpPost]
        public async Task<IActionResult> AddMedication(StockMedications medication)
        {
            try
            {
                bool exists = await _context.StockMedications
                    .AnyAsync(m => m.Name == medication.Name && m.Brand == medication.Brand && m.IsActive);

                if (exists)
                {
                    ModelState.AddModelError("", "This medication with the same name and brand already exists.");
                }
                else
                {
                    _context.Add(medication);
                    await _context.SaveChangesAsync();
                    TempData["ToastMessage"] = "Medication added successfully!";
                    TempData["ToastType"] = "success";
                    return RedirectToAction(nameof(ViewMedication));
                }
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, contact your system administrator.");
                TempData["ToastMessage"] = "Unable to save. Try again!";
                TempData["ToastType"] = "danger";
            }

            // Repopulate the category list for the view
            ViewBag.Categories = _context.MedicationCategory.ToList();
            var medications = _context.StockMedications.Include(m => m.MedicationCategory).Where(m => m.IsActive).ToList();
            return View("ViewMedication", medications);
        }
        //Edit Medication
        [HttpGet]
        public async Task<IActionResult> EditMedication(int id)
        {
            var medication = await _context.StockMedications.FindAsync(id);
            if (medication == null || !medication.IsActive)
            {
                return NotFound();
            }

            ViewBag.Categories = _context.MedicationCategory.ToList();
            return View(medication);
        }

        // POST: Edit Medication
        [HttpPost]
        public async Task<IActionResult> EditMedication(StockMedications medication)
        {
            try
            {
                _context.Update(medication);
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "Medication updated successfully!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(ViewMedication));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to update. Try again, or contact the administrator.");
                TempData["ToastMessage"] = "Unable to save. Try again!";
                TempData["ToastType"] = "danger";
            }

            ViewBag.Categories = _context.MedicationCategory.ToList();
            var medications = _context.StockMedications.Include(m => m.MedicationCategory).Where(m => m.IsActive).ToList();
            return View("ViewMedication", medications);
        }

        //SoftDelete Medication
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDeleteMedication(int MedId)
        {
            Console.WriteLine("Soft delete requested for MedId: " + MedId);
            var medication = await _context.StockMedications.FindAsync(MedId);
            if (medication == null)
            {
                return NotFound();
            }

            medication.IsActive = false; // Soft delete
            _context.Update(medication);
            TempData["ToastMessage"] = "successfully Deleted!";
            TempData["ToastType"] = "success";
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewMedication));
        }

        //View Wards details
        public IActionResult WardDetails()
        {
            var wards = _context.wards.ToList();
            return View(wards);
        }

        //Add Ward Details
        [HttpPost]
        public async Task<IActionResult> AddWard(Wards ward)
        {
            if (ModelState.IsValid)
            {
                _context.wards.Add(ward);
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "successfully Added!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(WardDetails)); // Or wherever your ward list is shown
            }
            return View(ward);
        }

        //Edit ward details
        [HttpPost]
        public async Task<IActionResult> EditWard(Wards ward)
        {
            if (ModelState.IsValid)
            {
                _context.wards.Update(ward);
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "successfully Edited!";
                TempData["ToastType"] = "success";
                return RedirectToAction("WardDetails"); // Adjust to match your view
            }
            return View(ward);
        }

        //View List of Registerd Users
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RegisteredUsers(string gender = "", string ageGroup = "", string search = "", DateTime? startDate = null, DateTime? endDate = null, int pg = 1)
        {
            var activeUsers = await _userManager.Users
                .Where(u => u.IsActive)  // Only users where IsActive == true
                .ToListAsync();

            var staffList = new List<StaffListViewModel>();

            foreach (var user in activeUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Count == 1 && roles.Contains("User")) // Registered Users
                {
                    staffList.Add(new StaffListViewModel
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Roles = string.Join(", ", roles),
                        Age = user.Age,
                        Address = user.Address,
                        PhoneNumber = user.PhoneNumber,
                        IdNumber = user.IdNumber,
                        Gender = user.Gender,
                        DateAdded = user.DateAdded
                    });
                }
            }

            // Get all "User" role accounts for stats only (ignores filters)
            var allUsersForStats = (from u in _context.Users
                                    join ur in _context.UserRoles on u.Id equals ur.UserId
                                    join r in _context.Roles on ur.RoleId equals r.Id
                                    where r.Name == "User" && u.IsActive
                                    select new StaffListViewModel
                                    {
                                        Id = u.Id,
                                        FullName = u.FullName,
                                        Age = u.Age,
                                        Gender = u.Gender,
                                        Email = u.Email,
                                        PhoneNumber = u.PhoneNumber,
                                        Roles = r.Name,
                                        DateAdded = u.DateAdded
                                    }).ToList();

            ViewBag.AllUsersForStats = allUsersForStats;

            if (startDate.HasValue && endDate.HasValue)
            {
                staffList = staffList
                    .Where(s => s.DateAdded.Date >= startDate.Value.Date && s.DateAdded.Date <= endDate.Value.Date)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(gender))
            {
                staffList = staffList
                    .Where(s => s.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(ageGroup))
            {
                staffList = staffList.Where(s => ageGroup switch
                {
                    "young" => s.Age < 30,
                    "middle" => s.Age >= 30 && s.Age <= 50,
                    "senior" => s.Age > 50,
                    _ => true
                }).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                staffList = staffList
                    .Where(s => s.FullName.ToLower().Contains(search)
                             || s.Email.ToLower().Contains(search)
                             || s.Roles.ToLower().Contains(search)
                             || s.Address.ToLower().Contains(search)
                             || s.PhoneNumber.ToLower().Contains(search)
                             || s.IdNumber.ToLower().Contains(search)
                             || s.Gender.ToLower().Contains(search))
                    .ToList();
            }

            //Paging
            const int pageSize = 5;
            if (pg < 1)
            {
                pg = 1;
            }

            int recsCount = staffList.Count();
            var pager = new Pager(recsCount, pg, pageSize);
            int recSkip = (pg - 1) * pageSize;
            var data = staffList.Skip(recSkip).Take(pageSize).ToList();

            ViewBag.Pager = pager;
            ViewBag.CurrentGender = gender;
            ViewBag.CurrentAgeGroup = ageGroup;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentEndDate = endDate?.ToString("yyyy-MM-dd");

            return View(data);

        }
    }

}
