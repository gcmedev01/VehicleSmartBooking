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

    const renderBar = (canvas, labels, values) => {
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        destroyChart(canvas);

        new Chart(canvas, {
            type: "bar",
            data: {
                labels,
                datasets: [{
                    label: "Ratings",
                    data: values,
                    backgroundColor: "#0d6efd"
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
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

    const renderDonut = (canvas, labels, values) => {
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        destroyChart(canvas);

        new Chart(canvas, {
            type: "doughnut",
            data: {
                labels,
                datasets: [{
                    data: values,
                    backgroundColor: ["#198754", "#dc3545", "#6c757d"],
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: "bottom"
                    }
                }
            }
        });
    };

    const initWidget = async (widget) => {
        const ratingCanvas = widget.querySelector(".js-rating-dist-chart");
        const outcomeCanvas = widget.querySelector(".js-dispatch-outcome-chart");
        const avgEl = widget.querySelector(".js-rating-avg");
        const countEl = widget.querySelector(".js-rating-count");
        const reasonsEl = widget.querySelector(".js-decline-reasons");

        if (ratingCanvas) {
            const data = await fetchJson(ratingCanvas.dataset.ratingUrl);
            const buckets = data?.buckets ?? [];
            const labels = buckets.map(x => String(x.score));
            const values = buckets.map(x => x.count ?? 0);
            renderBar(ratingCanvas, labels, values);

            if (avgEl) {
                avgEl.textContent = (data?.averageRating ?? 0).toFixed(1);
            }
            if (countEl) {
                countEl.textContent = String(data?.ratingCount ?? 0);
            }
        }

        if (outcomeCanvas) {
            const data = await fetchJson(outcomeCanvas.dataset.outcomeUrl);
            const labels = ["Accepted", "Declined", "No Response"];
            const values = [data?.acceptedCount ?? 0, data?.declinedCount ?? 0, data?.noResponseCount ?? 0];
            renderDonut(outcomeCanvas, labels, values);

            if (reasonsEl) {
                reasonsEl.innerHTML = "";
                const reasons = data?.topDeclineReasons ?? [];
                if (!reasons.length) {
                    reasonsEl.innerHTML = "<li class=\"text-muted\">No decline reasons.</li>";
                } else {
                    reasons.forEach(reason => {
                        const li = document.createElement("li");
                        li.textContent = `${reason.reason} (${reason.count})`;
                        reasonsEl.appendChild(li);
                    });
                }
            }
        }
    };

    document.addEventListener("dashboard:widgetLoaded", (event) => {
        if (event.detail?.id !== "driver-quality") {
            return;
        }
        if (event.detail?.element) {
            void initWidget(event.detail.element);
        }
    });
})();
