function renderLedgerTransactionDescription(description, notes) {
    const cell = document.createElement('div');
    cell.textContent = description || '';
    if (notes && notes.trim()) {
        const details = document.createElement('details');
        details.className = 'mt-1';
        const summary = document.createElement('summary');
        summary.className = 'text-muted';
        summary.textContent = document.querySelector('#transaction-notes-loc-data [data-key="notes"]')?.textContent || 'Notes';
        const content = document.createElement('div');
        content.className = 'text-break';
        content.style.whiteSpace = 'pre-wrap';
        content.textContent = notes;
        details.append(summary, content);
        cell.append(details);
    }
    return cell.innerHTML;
}
