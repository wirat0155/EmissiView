// --- Configuration ---
const PROD_PLANTS = [
    { id: 'lp', name: 'LP', color: '#10b981' }, // Green
    { id: 'plating-sub', name: 'Plating - Sub', color: '#0ea5e9' }, // Sky
    { id: 'plating-greitmo', name: 'Plating - Greitmo', color: '#6366f1' }, // Indigo
    { id: 'brazing', name: 'Brazing', color: '#f97316' }, // Orange
    { id: 'dom', name: 'DOM', color: '#a855f7' }, // Purple
    { id: 'exp', name: 'EXP', color: '#ec4899' }, // Pink
    { id: 'exp2', name: 'EXP2', color: '#f43f5e' } // Rose
];

const COST_PLANTS = [
    { id: 'lp', name: 'LP', color: '#10b981' },
    { id: 'plating', name: 'Plating', color: '#0ea5e9' },
    { id: 'brazing', name: 'Brazing', color: '#f97316' }
];

// --- State Application ---
let state = {
    viewDate: new Date(2026, 0, 1), // Year for main graph, Month for modal
    showComparison: false,
    modalShowComparison: false,
    // 7 items for Production Chart (LP, Sub, Greitmo, Brazing, Dom, Exp, Exp2)
    visiblePlants: new Array(7).fill(true),
    currentPhase: '8' // Default Phase 8
};

const isPlantVisibleInPhase = (index) => {
    // Phase 8: Indices 0-3
    // Phase 4: Indices 4-6
    if (state.currentPhase === '8') {
        return index < 4 && state.visiblePlants[index];
    } else {
        return index >= 4 && state.visiblePlants[index];
    }
};

// Caches
const annualDataCache = {};
const dailyDataCache = {};

let mainChart;
let dailyChart;
let costChart;
let consumptionChart;
let emissionChart;

// --- Persistence Logic ---
const STORAGE_KEY = 'uic_chart_settings_v2'; // Bump version

const saveSettings = () => {
    const settings = {
        showComparison: state.showComparison,
        visiblePlants: state.visiblePlants,
        currentPhase: state.currentPhase
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
};

const loadSettings = () => {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
        try {
            const parsed = JSON.parse(saved);
            if (typeof parsed.showComparison === 'boolean') {
                state.showComparison = parsed.showComparison;
                const toggle = document.getElementById('compareToggle');
                if (toggle) toggle.checked = state.showComparison;
            }
            if (Array.isArray(parsed.visiblePlants) && parsed.visiblePlants.length === PROD_PLANTS.length) {
                state.visiblePlants = parsed.visiblePlants;
            }
            if (parsed.currentPhase) {
                state.currentPhase = parsed.currentPhase;
                const sel = document.getElementById('phaseSelector');
                if (sel) sel.value = state.currentPhase;
            }
        } catch (e) {
            console.error('Failed to load settings', e);
        }
    }
};

// --- DATA LOGIC: ANNUAL (Main Graph) ---
// Helper: Process API Data to Series
const processApiDataToMonthly = (apiData, targetYear) => {
    const monthlyData = {};
    // Init buckets
    PROD_PLANTS.forEach(p => monthlyData[p.id.toLowerCase()] = new Array(12).fill(0));

    if (Array.isArray(apiData)) {
        apiData.forEach(item => {
            const date = new Date(item.tranDate);
            // We trust the API to return data within the requested range.
            // Using getMonth() directly avoids strict Year matching issues if timezone shifts Jan 1 to Dec 31 prev year 
            // (though we should strictly handle timezone, for this dashboard context relying on returned month index is safer if range is correct).
            // However, to be safe against gross errors, we can check if it's "close" to targetYear, but let's just use month.
            const month = date.getMonth(); 
            const p = item.plant ? item.plant.toLowerCase() : '';
            if (monthlyData[p]) {
                const qty = item.tranQty ? parseFloat(item.tranQty) : 0;
                monthlyData[p][month] += qty;
            }
        });
    }

    return PROD_PLANTS.map(plant => ({
        id: plant.id,
        name: plant.name,
        color: plant.color,
        data: monthlyData[plant.id.toLowerCase()] || new Array(12).fill(0)
    }));
};

const fetchAnnualData = async (year) => {
    const key = String(year);
    if (annualDataCache[key]) return annualDataCache[key];

    const prevYear = year - 1;

    try {
        const startCurrent = `${year}-01-01`;
        const endCurrent = `${year}-12-31`;
        
        const startPrev = `${prevYear}-01-01`;
        const endPrev = `${prevYear}-12-31`;

        // Parallel Fetch
        const [resCurrent, resPrev] = await Promise.all([
            fetch(`${basePath}/ElectricChart/GetProductionData?startDate=${startCurrent}&endDate=${endCurrent}`),
            fetch(`${basePath}/ElectricChart/GetProductionData?startDate=${startPrev}&endDate=${endPrev}`)
        ]);

        const dataCurrent = resCurrent.ok ? await resCurrent.json() : [];
        const dataPrev = resPrev.ok ? await resPrev.json() : [];

        const plantData = processApiDataToMonthly(dataCurrent, year);
        const lastYearPlantData = processApiDataToMonthly(dataPrev, prevYear);

        const result = { plantData, lastYearPlantData };
        annualDataCache[key] = result;
        return result;

    } catch (e) {
        console.error("Failed to fetch annual data", e);
        // Fallback to 0s
        const empty = PROD_PLANTS.map(p => ({
            id: p.id, name: p.name, color: p.color, data: new Array(12).fill(0)
        }));
        return { plantData: empty, lastYearPlantData: empty };
    }
};

const calculateAnnualLastYearTotal = (dataSet) => {
    const totalData = new Array(12).fill(0);
    dataSet.lastYearPlantData.forEach((plantSeries, index) => {
        if (state.visiblePlants[index]) {
            for (let i = 0; i < 12; i++) {
                totalData[i] += plantSeries.data[i];
            }
        }
    });
    return totalData;
};

// --- DATA LOGIC: DAILY (Modal Graph) ---
// --- DATA LOGIC: DAILY (Modal Graph) ---
const formatDateKey = (date) => {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
};

const fetchDailyData = async (date) => {
    const key = formatDateKey(date);
    if (dailyDataCache[key]) return dailyDataCache[key];

    const year = date.getFullYear();
    const month = date.getMonth() + 1;
    const lastDay = new Date(year, month, 0).getDate();
    const daysInMonth = lastDay;

    // Dates for API
    const startDate = `${year}-${String(month).padStart(2, '0')}-01`;
    const endDate = `${year}-${String(month).padStart(2, '0')}-${lastDay}`;

    // Mock generator for fallback (none expected for PROD_PLANTS now)

    try {
        const response = await fetch(`${basePath}/ElectricChart/GetProductionData?startDate=${startDate}&endDate=${endDate}`);
        const apiData = await response.ok ? await response.json() : [];

        // Map API data to Plant dict
        const productionMap = {};
        PROD_PLANTS.forEach(p => {
             productionMap[p.id.toLowerCase()] = {};
        });

        apiData.forEach(item => {
            const d = new Date(item.tranDate);
            const day = d.getDate();
            const p = item.plant ? item.plant.toLowerCase() : '';
            
            if (productionMap[p]) {
                productionMap[p][day] = item.tranQty;
            }
        });

        const plantData = PROD_PLANTS.map(plant => {
            const pid = plant.id.toLowerCase();
            // All PROD plants now have real data logic
            if (productionMap[pid]) {
                return {
                    id: plant.id,
                    name: plant.name,
                    color: plant.color,
                    data: Array.from({ length: daysInMonth }, (_, i) => {
                        return productionMap[pid][i + 1] || 0;
                    })
                };
            } else {
                // Fallback (should not happen if backend is correct)
                return {
                    id: plant.id,
                    name: plant.name,
                    color: plant.color,
                    data: Array.from({ length: daysInMonth }, () => 0)
                };
            }
        });

        const result = { daysInMonth, plantData };
        dailyDataCache[key] = result;
        return result;

    } catch (error) {
        console.error("Error fetching daily data:", error);
        // Fallback or empty
        return {
            daysInMonth,
            plantData: PROD_PLANTS.map(p => ({ ...p, data: Array.from({ length: daysInMonth }, () => 0) }))
        };
    }
};

// --- MAIN CHART RENDERING (Annual) ---
const initMainChart = () => {
    const options = {
        series: [],
        chart: {
            height: 450,
            type: 'bar', // Columns for monthly view looks better
            stacked: true,
            fontFamily: 'Outfit, sans-serif',
            toolbar: { show: false },
            animations: { enabled: true },
            events: {
                legendClick: function (chartContext, seriesIndex, config) {
                    if (seriesIndex < PROD_PLANTS.length) {
                        state.visiblePlants[seriesIndex] = !state.visiblePlants[seriesIndex];
                        saveSettings();
                        updateDashboard();
                        return false;
                    }
                    return true;
                }
            }
        },
        plotOptions: {
            bar: {
                columnWidth: '55%',
                borderRadius: 4,
                dataLabels: {
                    total: {
                        enabled: true,
                        style: {
                            fontSize: '12px', fontWeight: 700, color: '#475569'
                        },
                        formatter: (val) => val >= 1000 ? (val / 1000).toFixed(1) + 'k' : val.toLocaleString(),
                        offsetY: -8
                    }
                }
            }
        },
        dataLabels: { enabled: false },
        xaxis: {
            categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
            axisBorder: { show: false },
            axisTicks: { show: false },
            labels: { style: { colors: '#64748b' } }
        },
        yaxis: {
            title: { text: 'Units Produced', style: { color: '#64748b' } },
            labels: {
                style: { colors: '#64748b' },
                formatter: (val) => val >= 1000 ? (val / 1000).toFixed(0) + 'k' : val.toFixed(0)
            },
        },
        fill: { opacity: 1 },
        colors: [...PROD_PLANTS.map(p => p.color), '#ef4444'],
        legend: {
            position: 'top',
            horizontalAlign: 'right',
            markers: { radius: 12 },
            inverseOrder: true
        },
        tooltip: {
            shared: true,
            intersect: false,
            y: {
                formatter: (y) => typeof y !== "undefined" ? y.toLocaleString() + " units" : y
            }
        },
        grid: { borderColor: '#f1f5f9' }
    };

    mainChart = new ApexCharts(document.querySelector("#chart"), options);
    mainChart.render();
};

const updateDashboard = async () => {
    const year = state.viewDate.getFullYear();
    const month = state.viewDate.getMonth() + 1;

    // Sync Global Input
    const globalInput = document.getElementById('globalDateInput');
    if (globalInput) {
        const fmt = `${year}-${String(month).padStart(2, '0')}`;
        if (globalInput.value !== fmt) globalInput.value = fmt;
    }

    // Check future year
    const isFuture = year > 2026; // Demo constraint
    if (isFuture) {
        document.getElementById('noDataOverlay').classList.add('active');
        document.getElementById('chart').style.opacity = '0.1';
        document.getElementById('summaryGrid').innerHTML = '';
        // Also update cost chart to empty or same year
        // Also update cost chart to empty or same year
        updateSecondaryCharts(year);
        return;
    } else {
        document.getElementById('noDataOverlay').classList.remove('active');
        document.getElementById('chart').style.opacity = '1';
    }
    
    // Show Loading
    const loadingOverlay = document.getElementById('loadingOverlay');
    if(loadingOverlay) loadingOverlay.classList.add('active');

    try {
        const dataSet = await fetchAnnualData(year);

        let finalSeries = dataSet.plantData.map((p, idx) => ({
            name: p.name,
            type: 'bar',
            data: p.data,
            hidden: !isPlantVisibleInPhase(idx)
        }));

        if (state.showComparison) {
            // Recalculate Last Year Total based on Phase Visibility
            // The API returns all 7. We must filter sum.
            const lastYearTotalData = new Array(12).fill(0);
            dataSet.lastYearPlantData.forEach((p, idx) => {
                 if (isPlantVisibleInPhase(idx)) {
                     p.data.forEach((val, mIdx) => {
                         lastYearTotalData[mIdx] += val;
                     });
                 }
            });

            finalSeries.push({
                name: `Total (${year - 1})`,
                type: 'area',
                data: lastYearTotalData
            });
        }

        // Dynamic Options construction
        const fillType = finalSeries.map(s => s.type === 'area' ? 'gradient' : 'solid');
        const fillOpacity = finalSeries.map(s => s.type === 'area' ? 0.4 : 1);
        const strokeWidths = finalSeries.map(s => s.type === 'area' ? 2 : 0);

        // Chart update
        mainChart.updateOptions({
            stroke: {
                width: strokeWidths,
                curve: 'smooth'
            },
            fill: {
                type: fillType,
                opacity: fillOpacity,
                gradient: {
                    shade: 'light',
                    type: "vertical",
                    shadeIntensity: 0.5,
                    opacityFrom: 0.5,
                    opacityTo: 0.1,
                    stops: [0, 100]
                }
            }
        });
        mainChart.updateSeries(finalSeries);

        // Summary Update (Total for the Year)
        updateSummary(dataSet.plantData);
        
    } finally {
        // Hide Loading
        if(loadingOverlay) loadingOverlay.classList.remove('active');
    }

    // Cost/Cons/CO2 Charts Update
    updateSecondaryCharts(year);

    // Also update detail charts if they are open/active (or just update them always to be safe)
    if (dailyChart) updateDailyChart();
};

const updateSummary = (plantSeries) => {
    const container = document.getElementById('summaryGrid');
    container.innerHTML = '';
    let grandTotal = 0;

    plantSeries.forEach(series => {
        const total = series.data.reduce((a, b) => a + b, 0);
        grandTotal += total;

        const card = document.createElement('div');
        card.className = 'summary-card';
        card.innerHTML = `
            <div class="summary-title">
                <span class="dot" style="background-color: ${series.color}"></span>
                ${series.name} (Year)
            </div>
            <div class="summary-value">${total.toLocaleString()}</div>
        `;
        container.appendChild(card);
    });

    const totalCard = document.createElement('div');
    totalCard.className = 'summary-card total';
    totalCard.innerHTML = `
        <div class="summary-title">Grand Total</div>
        <div class="summary-value">${grandTotal.toLocaleString()}</div>
    `;
    container.appendChild(totalCard);
};

// --- MODAL CHART RENDERING (Daily) ---
const initDailyChart = () => {
    const options = {
        series: [],
        chart: {
            height: 400,
            type: 'bar',
            stacked: true,
            fontFamily: 'Outfit, sans-serif',
            toolbar: { show: false },
            animations: { enabled: true },
            events: {
                legendClick: function (chartContext, seriesIndex, config) {
                    if (seriesIndex < PROD_PLANTS.length) {
                        state.visiblePlants[seriesIndex] = !state.visiblePlants[seriesIndex];
                        saveSettings();
                        updateDailyChart();
                        updateDashboard();
                        return false;
                    }
                    return true;
                }
            }
        },
        stroke: {
            width: [0, 0, 0, 4],
            curve: 'smooth'
        },
        plotOptions: {
            bar: {
                borderRadius: 4,
                dataLabels: {
                    total: {
                        enabled: true,
                        style: {
                            fontSize: '11px', fontWeight: 700, color: '#475569'
                        },
                        formatter: (val) => val >= 1000 ? (val / 1000).toFixed(1) + 'k' : val.toLocaleString(),
                        offsetY: -8
                    }
                }
            }
        },
        dataLabels: { enabled: false },
        xaxis: {
            categories: [], // 1-31
            axisBorder: { show: false },
            axisTicks: { show: false }
        },
        yaxis: {
            labels: {
                formatter: (val) => val >= 1000 ? (val / 1000).toFixed(1) + 'k' : val.toFixed(0)
            }
        },
        // Add color for Last Year (Red) to the array
        colors: [...PROD_PLANTS.map(p => p.color), '#ef4444'],
        legend: {
            position: 'top',
            horizontalAlign: 'right',
            inverseOrder: true // Ensures Last Year (last in series) is first in legend
        },
        tooltip: {
            shared: true,
            intersect: false,
            y: {
                formatter: (y) => typeof y !== "undefined" ? y.toLocaleString() : y
            }
        },
        grid: { borderColor: '#f1f5f9' }
    };

    dailyChart = new ApexCharts(document.querySelector("#dailyChart"), options);
    dailyChart.render();
};

const updateDailyChart = async () => {
    // Note: Modal now follows GLOBAL state.viewDate
    const loadingOverlay = document.getElementById('modalLoadingOverlay');
    if (loadingOverlay) loadingOverlay.classList.add('active');

    try {
        const promises = [fetchDailyData(state.viewDate)];
        if (state.modalShowComparison) {
            const lastYearDate = new Date(state.viewDate);
            lastYearDate.setFullYear(lastYearDate.getFullYear() - 1);
            promises.push(fetchDailyData(lastYearDate));
        }

        const results = await Promise.all(promises);
        const dataSet = results[0];
        const lastYearSet = results.length > 1 ? results[1] : null;

        const daysArray = Array.from({ length: dataSet.daysInMonth }, (_, i) => i + 1);
    
        let series = dataSet.plantData.map((p, idx) => ({
            name: p.name,
            type: 'bar',
            data: p.data,
            // Apply global visibility
            hidden: !isPlantVisibleInPhase(idx)
        }));
    
        if (lastYearSet) {
            // Calculate Total based on VISIBLE plants
            const totalData = new Array(dataSet.daysInMonth).fill(0);
            lastYearSet.plantData.forEach((p, idx) => {
                if (isPlantVisibleInPhase(idx)) {
                    // We map strictly to current month days. 
                    p.data.forEach((val, dayIdx) => {
                        if (dayIdx < totalData.length) totalData[dayIdx] += val;
                    });
                }
            });
    
            const lastYear = new Date(state.viewDate).getFullYear() - 1;
            series.push({
                name: `Last Year (${lastYear})`,
                type: 'area', // Changed to area
                data: totalData
            });
        }
    
        // Dynamic Options construction
        const fillType = series.map(s => s.type === 'area' ? 'gradient' : 'solid');
        const fillOpacity = series.map(s => s.type === 'area' ? 0.4 : 1);
        const strokeWidths = series.map(s => s.type === 'area' ? 2 : 0);

        dailyChart.updateOptions({
            xaxis: { categories: daysArray },
            stroke: {
                width: strokeWidths,
                curve: 'smooth'
            },
            fill: {
                type: fillType,
                opacity: fillOpacity,
                gradient: {
                    shade: 'light',
                    type: "vertical",
                    shadeIntensity: 0.5,
                    opacityFrom: 0.5,
                    opacityTo: 0.1,
                    stops: [0, 90]
                }
            }
        });
        dailyChart.updateSeries(series);
    } finally {
        if (loadingOverlay) loadingOverlay.classList.remove('active');
    }
};

// --- SECONDARY CHARTS (Cost, Consumption, CO2) ---
const secondaryCache = {};

// Helper to generate data for Cost, KW, CO2
const generateSecondaryData = (year) => {
    const key = String(year);
    if (secondaryCache[key]) return secondaryCache[key];

    // Mock Data based on typical production patterns (Seasonality: High in Q4, Low in Q2)
    // LP: Low consumption, Plating: Medium, Brazing: High (Ovens)

    // Base profiles (kWh per month average)
    const baseCons = { 'lp': 8000, 'plating': 25000, 'brazing': 65000 };
    const seasonalFactors = [0.9, 0.95, 1.1, 1.0, 0.9, 0.9, 1.0, 1.05, 1.15, 1.25, 1.2, 1.1]; // Jan-Dec

    // Cost per Unit (THB/kWh) - Fluctuates slightly
    const ftRate = [4.2, 4.2, 4.2, 4.2, 4.5, 4.5, 4.5, 4.5, 4.7, 4.7, 4.7, 4.7]; // Example rate changes

    // Emission Factor (kgCO2e/kWh) - Constant for grid electricity
    const emissionFactor = 0.4999;

    // Consumption (kWh)
    const consData = COST_PLANTS.map(plant => ({
        name: plant.name,
        color: plant.color,
        data: seasonalFactors.map(factor => {
            const base = baseCons[plant.id];
            // Add some randomness +/- 5%
            const variance = (Math.random() * 0.1) + 0.95;
            return parseFloat((base * factor * variance).toFixed(0));
        })
    }));

    // Cost (THB) = Consumption * Rate
    const costData = COST_PLANTS.map((plant, idx) => ({
        name: plant.name,
        color: plant.color,
        data: consData[idx].data.map((kwh, mIdx) => {
            return parseFloat((kwh * ftRate[mIdx]).toFixed(2));
        })
    }));

    // CO2 (kg) = Consumption * Factor
    const co2Data = COST_PLANTS.map((plant, idx) => ({
        name: plant.name,
        color: plant.color,
        data: consData[idx].data.map(kwh => {
            return parseFloat((kwh * emissionFactor).toFixed(2));
        })
    }));

    const result = { costData, consData, co2Data };
    secondaryCache[key] = result;
    return result;
};

// Generic Chart Options Factory
const getSecondaryChartOptions = (unit) => ({
    series: [],
    chart: {
        height: 300,
        type: 'bar',
        stacked: true,
        fontFamily: 'Outfit, sans-serif',
        toolbar: { show: false },
        events: {
            legendClick: function (chartContext, seriesIndex, config) {
                // Map Cost Index to VisiblePlants Index
                // 0 (LP) -> 0
                // 1 (Plating) -> Has no direct toggle in Prod, but let's toggle index 1 (Sub) arbitrarily or ignore?
                // The request said "Synchonized Legend".
                // Let's implement Mapping:
                // COST[0] (LP) <-> PROD[0] (LP)
                // COST[1] (Plating) <-> PROD[1] (Sub) AND PROD[2] (Greitmo)
                // COST[2] (Brazing) <-> PROD[3] (Brazing)
                
                if (seriesIndex === 0) { // LP
                    state.visiblePlants[0] = !state.visiblePlants[0];
                } else if (seriesIndex === 1) { // Plating
                    // Toggle BOTH sub and greitmo
                    const newState = !(state.visiblePlants[1] || state.visiblePlants[2]);
                    state.visiblePlants[1] = newState;
                    state.visiblePlants[2] = newState;
                } else if (seriesIndex === 2) { // Brazing
                    state.visiblePlants[3] = !state.visiblePlants[3];
                }
                
                saveSettings();
                updateDashboard();
                return false;
            }
        }
    },
    plotOptions: {
        bar: {
            columnWidth: '50%',
            borderRadius: 4,
            dataLabels: {
                total: {
                    enabled: true,
                    style: { fontSize: '11px', fontWeight: 600 },
                    formatter: (val) => val >= 1000 ? (val / 1000).toFixed(1) + 'k' : val.toFixed(0)
                }
            }
        }
    },
    dataLabels: { enabled: false },
    xaxis: {
        categories: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
        axisBorder: { show: false },
        axisTicks: { show: false }
    },
    yaxis: {
        title: { text: unit },
        labels: { formatter: (val) => val >= 1000 ? (val / 1000).toFixed(0) + 'k' : val.toFixed(0) }
    },
    colors: COST_PLANTS.map(p => p.color),
    legend: { position: 'top', horizontalAlign: 'right', inverseOrder: true },
    tooltip: { shared: true, intersect: false },
    grid: { borderColor: '#f1f5f9' },
    noData: {
        text: 'No Data Available',
        align: 'center',
        verticalAlign: 'middle',
        style: {
            color: '#64748b',
            fontSize: '16px',
            fontFamily: 'Outfit, sans-serif'
        }
    }
});

const initSecondaryCharts = () => {
    costChart = new ApexCharts(document.querySelector("#costChart"), getSecondaryChartOptions('THB'));
    costChart.render();

    consumptionChart = new ApexCharts(document.querySelector("#consumptionChart"), getSecondaryChartOptions('kWh'));
    consumptionChart.render();

    emissionChart = new ApexCharts(document.querySelector("#emissionChart"), getSecondaryChartOptions('kgCO₂e'));
    emissionChart.render();
};

const updateSecondaryCharts = (year) => {
    document.getElementById('costYearLabel').textContent = year;

    // Future Check
    const isFuture = year > 2026;
    if (isFuture) {
        [costChart, consumptionChart, emissionChart].forEach(c => c.updateSeries([]));
        return;
    }

    const data = generateSecondaryData(year);

    // Helper to filter hidden plants based on mapping
    // COST[0] (LP) -> visiblePlants[0]
    // COST[1] (Plating) -> visiblePlants[1] || visiblePlants[2]
    // COST[2] (Brazing) -> visiblePlants[3]
    const getVisibleSeries = (sourceData) => sourceData.map((s, idx) => {
        let isVisible = true;
        if (idx === 0) isVisible = state.visiblePlants[0];
        else if (idx === 1) isVisible = state.visiblePlants[1] || state.visiblePlants[2];
        else if (idx === 2) isVisible = state.visiblePlants[3];
        
        return {
            ...s,
            hidden: !isVisible
        };
    });

    costChart.updateSeries(getVisibleSeries(data.costData));
    consumptionChart.updateSeries(getVisibleSeries(data.consData));
    emissionChart.updateSeries(getVisibleSeries(data.co2Data));
};

// No longer window.updateShowState needed
window.updateShowState = () => { };


// --- EVENT LISTENERS ---

// --- EVENT LISTENERS ---

// Global Month/Year Navigation


document.getElementById('globalDateInput').addEventListener('change', (e) => {
    if (!e.target.value) return;
    const [y, m] = e.target.value.split('-').map(Number);
    state.viewDate.setFullYear(y);
    state.viewDate.setMonth(m - 1);
    updateDashboard();
});

// Comparison Toggle
document.getElementById('compareToggle').addEventListener('change', (e) => {
    state.showComparison = e.target.checked;
    saveSettings();
    updateDashboard();
});

// Modal Logic
const detailModal = document.getElementById('detailModal');
const openDetailBtn = document.getElementById('openDetailBtn');
const closeDetailBtn = document.getElementById('closeDetailBtn');

const openModal = () => {
    detailModal.classList.add('active');

    // Update Title with Date
    const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const currentYear = state.viewDate.getFullYear();
    const currentMonthName = monthNames[state.viewDate.getMonth()];

    const titleEl = detailModal.querySelector('.modal-title h2');
    if (titleEl) titleEl.textContent = `Daily FG Production - ${currentMonthName} ${currentYear}`;

    if (!dailyChart) initDailyChart();

    // Update Daily Chart to match current View Year + Month (defaulting to Jan if not tracked, but we track full Date)
    // Actually, let's keep the month in state.viewDate as well.
    updateDailyChart();
};

const closeModal = () => {
    detailModal.classList.remove('active');
};

openDetailBtn.addEventListener('click', openModal);
closeDetailBtn.addEventListener('click', closeModal);
detailModal.addEventListener('click', (e) => {
    if (e.target === detailModal) closeModal();
});

document.getElementById('modalCompareToggle').addEventListener('change', (e) => {
    state.modalShowComparison = e.target.checked;
    updateDailyChart();
    // Intentionally NOT saving this setting as requested
});





// --- SECONDARY MODAL LOGIC (Detail View) ---
let secChart1, secChart2, secChart3;
const secModal = document.getElementById('secondaryDetailModal');

// Reusable Options
const getDetailChartOptions = (type, unit) => ({
    series: [],
    chart: { type: 'line', height: 350, toolbar: { show: false } },
    stroke: { width: 3, curve: 'smooth' },
    plotOptions: { bar: { borderRadius: 4, columnWidth: '50%' } }, // If bar used
    yaxis: { title: { text: unit } },
    legend: { position: 'top', horizontalAlign: 'right', inverseOrder: true },
    colors: COST_PLANTS.map(p => p.color),
    grid: { borderColor: '#f1f5f9' }
});

const openSecondaryModal = (type) => {
    let title = '';
    let unit = '';
    if (type === 'cost') { title = 'Electricity Cost Analysis'; unit = 'THB'; }
    if (type === 'cons') { title = 'Electricity Consumption Analysis'; unit = 'kWh'; }
    if (type === 'co2') { title = 'CO₂ Emissions Analysis'; unit = 'kgCO₂e'; }

    // Format Date for Titles
    const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const currentYear = state.viewDate.getFullYear();
    const currentMonthName = monthNames[state.viewDate.getMonth()];

    document.getElementById('secModalTitle').textContent = title;
    // Set titles for sub-charts
    document.getElementById('secChartTitle1').textContent = `Daily Total ${unit} (${currentMonthName} ${currentYear})`;
    document.getElementById('secChartTitle2').textContent = `Avg ${unit} per Unit (Annual View - 1-12 ${currentYear})`;
    document.getElementById('secChartTitle3').textContent = `Avg ${unit} per Unit (Daily View - ${currentMonthName} ${currentYear})`;

    secModal.classList.add('active');

    // Init charts if first time
    if (!secChart1) {
        secChart1 = new ApexCharts(document.querySelector("#secChart1"), {
            ...getDetailChartOptions(type, unit),
            chart: { type: 'bar', stacked: true, height: 350, toolbar: { show: false } },
            plotOptions: {
                bar: {
                    borderRadius: 4,
                    columnWidth: '50%',
                    dataLabels: {
                        total: {
                            enabled: true,
                            style: { fontSize: '11px', fontWeight: 600, color: '#374151' },
                            formatter: (val) => val >= 1000 ? (val / 1000).toFixed(1) + 'k' : val.toLocaleString(),
                            offsetY: -8
                        }
                    }
                }
            },
            dataLabels: { enabled: false }
        });
        secChart1.render();
        secChart2 = new ApexCharts(document.querySelector("#secChart2"), getDetailChartOptions(type, unit));
        secChart2.render();
        secChart3 = new ApexCharts(document.querySelector("#secChart3"), getDetailChartOptions(type, unit));
        secChart3.render();
    } else {
        // Update Y-Axis titles
        [secChart1, secChart2, secChart3].forEach(c => c.updateOptions({ yaxis: { title: { text: unit } } }));
    }

    updateSecondaryDetailCharts(type);
};

const updateSecondaryDetailCharts = (type) => {
    // 1. Daily Total (Month View) - using modal viewDate logic or just random
    const rand = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
    const randFloat = (min, max) => (Math.random() * (max - min) + min);

    // Mock Data Gen
    const days = 30; // Simplify
    const daysArr = Array.from({ length: days }, (_, i) => i + 1);
    const monthsArr = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

    // Chart 1: Daily Total (Stacked Bar)
    const series1 = COST_PLANTS.map(p => ({
        name: p.name,
        type: 'bar',
        data: Array.from({ length: days }, () => rand(1000, 5000))
    }));
    secChart1.updateOptions({ xaxis: { categories: daysArr } });
    secChart1.updateSeries(series1);

    // Chart 2: Avg Per Unit (Annual 1-12) (Line)
    // Low value ~ 0.5 - 2.0
    const series2 = COST_PLANTS.map(p => ({
        name: p.name,
        type: 'line',
        data: Array.from({ length: 12 }, () => parseFloat(randFloat(0.2, 1.5).toFixed(3)))
    }));
    secChart2.updateOptions({ xaxis: { categories: monthsArr } });
    secChart2.updateSeries(series2);

    // Chart 3: Avg Per Unit (Daily 1-30) (Line)
    const series3 = COST_PLANTS.map(p => ({
        name: p.name,
        type: 'line',
        data: Array.from({ length: days }, () => parseFloat(randFloat(0.2, 1.5).toFixed(3)))
    }));
    secChart3.updateOptions({ xaxis: { categories: daysArr } });
    secChart3.updateSeries(series3);
};

document.getElementById('closeSecDetailBtn').addEventListener('click', () => {
    secModal.classList.remove('active');
});

// Expose to window for HTML onclick
window.openSecondaryModal = openSecondaryModal;

const changeMonth = (offset) => {
    // safer date mutation
    const newDate = new Date(state.viewDate);
    newDate.setDate(1); // Force to 1st to avoid overflow (e.g. Jan 31 -> Feb 28 -> Mar 3)
    newDate.setMonth(newDate.getMonth() + offset);
    state.viewDate = newDate;
    updateDashboard();
};

// --- INITIALIZATION ---
document.addEventListener('DOMContentLoaded', () => {
    loadSettings();

    // Initialize state from Calendar Input
    const globalInput = document.getElementById('globalDateInput');
    if (globalInput && globalInput.value) {
        const [y, m] = globalInput.value.split('-').map(Number);
        state.viewDate = new Date(y, m - 1, 1);
    }

    initMainChart();
    initSecondaryCharts();
    initDailyChart(); // Init Modal Chart
    updateDashboard();

    // Controls
    const prevBtn = document.getElementById('prevDateBtn');
    if (prevBtn) prevBtn.addEventListener('click', () => changeMonth(-1));

    const nextBtn = document.getElementById('nextDateBtn');
    if (nextBtn) nextBtn.addEventListener('click', () => changeMonth(1));
    
    if (globalInput) {
        globalInput.addEventListener('change', (e) => {
            if (e.target.value) {
                const parts = e.target.value.split('-');
                state.viewDate = new Date(parseInt(parts[0]), parseInt(parts[1]) - 1, 1);
                updateDashboard();
            }
        });
    }

    const compareToggle = document.getElementById('compareToggle');
    if (compareToggle) {
        compareToggle.addEventListener('change', (e) => {
            state.showComparison = e.target.checked;
            saveSettings();
            updateDashboard();
        });
    }

     // Phase Selector
     const phaseSelector = document.getElementById('phaseSelector');
     if (phaseSelector) {
         phaseSelector.value = state.currentPhase; // Set init value
         phaseSelector.addEventListener('change', (e) => {
             state.currentPhase = e.target.value;
             saveSettings();
             updateDashboard();
         });
     }

    // Modal Controls
    const detailModal = document.getElementById('detailModal');
    const openDetailBtn = document.getElementById('openDetailBtn');
    const closeDetailBtn = document.getElementById('closeDetailBtn');

    if (openDetailBtn) {
        openDetailBtn.addEventListener('click', () => {
            detailModal.classList.add('active');
            updateDailyChart();
        });
    }

    const closeModal = () => {
        detailModal.classList.remove('active');
        state.modalShowComparison = false;
        const modalToggle = document.getElementById('modalCompareToggle');
        if(modalToggle) modalToggle.checked = false;
    };

    if (closeDetailBtn) closeDetailBtn.addEventListener('click', closeModal);
    
    if (detailModal) {
        detailModal.addEventListener('click', (e) => {
            if (e.target === detailModal) closeModal();
        });
    }

    const modalCompareToggle = document.getElementById('modalCompareToggle');
    if (modalCompareToggle) {
        modalCompareToggle.addEventListener('change', (e) => {
            state.modalShowComparison = e.target.checked;
            updateDailyChart();
        });
    }
});
