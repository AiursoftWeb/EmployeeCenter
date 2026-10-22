(() => {
    const allZones = document.getElementById('AuditAllZones');
    const zoneList = document.getElementById('included-zone-list');
    const search = document.getElementById('domain-search');
    const rows = Array.from(document.querySelectorAll('#excluded-domain-list [data-domain]'));
    const noMatches = document.getElementById('no-domain-matches');
    if (!allZones || !zoneList || !search || !noMatches) return;

    const updateZones = () => { zoneList.hidden = allZones.checked; };
    const updateDomains = () => {
        const excluded = rows.filter(row => row.querySelector('input').checked).map(row => row.dataset.domain);
        const query = search.value.trim().toLowerCase();
        for (const row of rows) {
            row.hidden = !row.dataset.domain.includes(query);
            row.classList.toggle('d-flex', !row.hidden);
            row.querySelector('[data-covered]').hidden = !excluded.some(parent => row.dataset.domain.endsWith(`.${parent}`));
        }
        noMatches.hidden = rows.some(row => !row.hidden);
    };
    allZones.addEventListener('change', updateZones);
    search.addEventListener('input', updateDomains);
    rows.forEach(row => row.querySelector('input').addEventListener('change', updateDomains));
    updateZones();
    updateDomains();
})();
