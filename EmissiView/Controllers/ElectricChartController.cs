using Microsoft.AspNetCore.Mvc;
using EmissiView.Repositories;
using System;
using System.Threading.Tasks;

namespace EmissiView.Controllers
{
    public class ElectricChartController : Controller
    {
        private readonly ElectricChartRepository _repository;

        public ElectricChartController(ElectricChartRepository repository)
        {
            _repository = repository;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetProductionData(DateTime startDate, DateTime endDate)
        {
            try
            {
                var data = await _repository.GetProductionDataAsync(startDate, endDate);
                return Json(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving production data.", error = ex.Message });
            }
        }
    }
}
