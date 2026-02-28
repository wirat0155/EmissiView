using Microsoft.AspNetCore.Mvc;
using EmissiView.Services;
using System;
using System.Threading.Tasks;

namespace EmissiView.Controllers
{
    public class ElectricChartController : Controller
    {
        private readonly IElectricChartRepository _repository;

        public ElectricChartController(IElectricChartRepository repository)
        {
            _repository = repository;
        }

        public IActionResult Index()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
