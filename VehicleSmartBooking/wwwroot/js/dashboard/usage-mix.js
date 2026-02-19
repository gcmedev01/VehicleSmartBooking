(() => {
    const buildUrl = (baseUrl) => {
        if (!baseUrl) {
            return "";
        }
        const query = window.location.search;
        if (!query) {
            return baseUrl;
        }
        return baseUrl.includes("?") ? `${baseUrl}&${query.substring(1)}` : `${baseUrl}${query}`;
    };

    const fetchJson = async (url) => {
        if (!url) {
            return null;
        }
        const response = await fetch(buildUrl(url), {
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json"
            }
        });
        if (!response.ok) {
            return null;
        }
        return response.json();
    };

    const destroyChart = (canvas) => {
        const existing = Chart.getChart(canvas);
        if (existing) {
            existing.destroy();
        }
    };

    const renderStackedBar = (canvas, labels, datasets) => {
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        destroyChart(canvas);

        new Chart(canvas, {
            type: "bar",
            data: {
                labels,
                datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        stacked: true
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true
                    }
                },
                plugins: {
                    legend: {
                        display: false
                    }
                }
            }
        });
    };

    const renderUsageMix = (canvas, data) => {
        const months = data?.months ?? [];
        const labels = months.map(x => x.label);
        const fleet = months.map(x => x.fleetTrips ?? 0);
        const vendor = months.map(x => x.vendorTrips ?? 0);
        const personal = months.map(x => x.personalTrips ?? 0);

        renderStackedBar(canvas, labels, [
            { label: "Fleet", data: fleet, backgroundColor: "#0d6efd" },
            { label: "Vendor", data: vendor, backgroundColor: "#198754" },
            { label: "Personal", data: personal, backgroundColor: "#ffc107" }
        ]);
    };

    const renderTripScope = (canvas, data) => {
        const months = data?.months ?? [];
        const labels = months.map(x => x.label);
        const inTrips = months.map(x => x.inProvinceTrips ?? 0);
        const outTrips = months.map(x => x.outProvinceTrips ?? 0);

        renderStackedBar(canvas, labels, [
            { label: "In Province", data: inTrips, backgroundColor: "#0dcaf0" },
            { label: "Out Province", data: outTrips, backgroundColor: "#6c757d" }
        ]);
    };

    const initWidget = async (widget) => {
        const usageCanvas = widget.querySelector(".js-usage-mix-chart");
        const tripCanvas = widget.querySelector(".js-trip-scope-chart");

        if (usageCanvas) {
            const usageUrl = usageCanvas.dataset.usageUrl;
            const usageData = await fetchJson(usageUrl);
            renderUsageMix(usageCanvas, usageData);
        }

        if (tripCanvas) {
            const tripUrl = tripCanvas.dataset.tripUrl;
            const tripData = await fetchJson(tripUrl);
            renderTripScope(tripCanvas, tripData);
        }
    };

    document.addEventListener("dashboard:widgetLoaded", (event) => {
        if (event.detail?.id !== "usage-mix") {
            return;
        }
        if (event.detail?.element) {
            void initWidget(event.detail.element);
        }
    });
})();
