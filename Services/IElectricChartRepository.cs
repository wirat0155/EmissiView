using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmissiView.Models;

namespace EmissiView.Services
{
    public interface IElectricChartRepository
    {
        Task<IEnumerable<ProductionDataModel>> GetProductionDataAsync(DateTime startDate, DateTime endDate);
    }
}
