(() => {
    'use strict';
    const page = document.getElementById('sharing-page');
    if (!page) return;
    const text = (key, fallback) => page.querySelector(`[data-key="${key}"]`)?.textContent ?? fallback;
    const addDetails = document.getElementById('add-share-details');
    const addForm = document.getElementById('add-share-form');
    const roleSelect = document.getElementById('roleSelect');
    const userSearch = document.getElementById('userSearch');
    const userId = document.getElementById('selectedUserId');
    const userDisplay = document.getElementById('selectedUserDisplay');
    const results = document.getElementById('userSearchResults');
    const searchStatus = document.getElementById('user-search-status');
    const visibilityForm = document.getElementById('visibility-form');
    const linkAccess = document.getElementById('link-access');
    let previewRequest;
    roleSelect.addEventListener('change', async () => {
        previewRequest?.abort();
        const request = new AbortController();
        previewRequest = request;
        const preview = document.getElementById('role-preview');
        const status = document.getElementById('role-preview-status');
        preview.hidden = true;
        status.textContent = '';
        if (!roleSelect.value) return;
        try {
            const response = await fetch(`/api/roles/${encodeURIComponent(roleSelect.value)}/info`, { signal: request.signal });
            if (!response.ok || response.redirected) throw new Error('Preview failed');
            const data = await response.json();
            if (request.signal.aborted) return;
            document.getElementById('role-preview-count').textContent = data.totalMembers;
            const members = document.getElementById('role-preview-members');
            members.replaceChildren();
            for (const member of data.previewMembers) {
                const label = document.createElement('span');
                label.textContent = member.displayName;
                members.append(label);
            }
            preview.hidden = false;
        } catch {
            if (!request.signal.aborted) status.textContent = text('roleFailed', 'Member preview unavailable. You can still share with this role.');
        }
    });
    let searchTimeout;
    let searchRequest;

    function showError(form, message) {
        const details = form.closest('details');
        if (details) details.open = true;
        const error = form.querySelector('.form-error');
        error.textContent = message;
        error.hidden = false;
    }

    page.querySelectorAll('input[name="shareType"]').forEach(radio => radio.addEventListener('change', () => {
        const isUser = document.getElementById('shareTypeUser').checked;
        document.getElementById('role-selection').hidden = isUser;
        document.getElementById('user-selection').hidden = !isUser;
        roleSelect.disabled = isUser;
        userSearch.disabled = !isUser;
        userId.disabled = !isUser;
        clearTimeout(searchTimeout);
        searchRequest?.abort();
        results.classList.add('d-none');
        searchStatus.textContent = '';
    }));

    userSearch.addEventListener('input', () => {
        clearTimeout(searchTimeout);
        searchRequest?.abort();
        results.replaceChildren();
        results.classList.add('d-none');
        searchStatus.textContent = '';
        userId.value = '';
        userDisplay.classList.add('d-none');
        const query = userSearch.value.trim();
        if (query.length < 3) return;
        searchTimeout = setTimeout(() => loadUsers(query), 300);
    });

    async function loadUsers(query, pageNumber = 1) {
        const request = new AbortController();
        searchRequest = request;
        searchStatus.textContent = text('searching', 'Searching...');
        try {
            const response = await fetch(`/api/users/search?query=${encodeURIComponent(query)}&page=${pageNumber}&pageSize=10`, { signal: request.signal });
            if (!response.ok || response.redirected) throw new Error('Search failed');
            const data = await response.json();
            if (request.signal.aborted || query !== userSearch.value.trim() || userSearch.disabled) return;
            if (pageNumber === 1) results.replaceChildren();
            results.querySelector('[data-load-more]')?.remove();
            for (const user of data.users) {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'list-group-item list-group-item-action text-break';
                const name = document.createElement('strong');
                name.textContent = user.displayName;
                const username = document.createElement('div');
                username.className = 'small text-muted';
                username.textContent = user.userName;
                button.append(name, username);
                button.addEventListener('click', () => {
                    userId.value = user.id;
                    document.getElementById('selectedUserName').textContent = user.displayName;
                    document.getElementById('selectedUserUsername').textContent = user.userName;
                    userDisplay.classList.remove('d-none');
                    results.classList.add('d-none');
                    userSearch.value = user.displayName;
                    userSearch.classList.add('d-none');
                    searchStatus.textContent = '';
                    document.getElementById('clearSelectedUser').focus();
                });
                results.append(button);
            }
            if (pageNumber < Math.ceil(data.totalCount / 10)) {
                const more = document.createElement('button');
                more.type = 'button';
                more.className = 'list-group-item list-group-item-action text-primary';
                more.dataset.loadMore = 'true';
                more.textContent = text('moreUsers', 'Load more users');
                more.addEventListener('click', () => { more.disabled = true; loadUsers(query, pageNumber + 1); });
                results.append(more);
            }
            results.classList.toggle('d-none', !results.children.length);
            searchStatus.textContent = data.users.length ? '' : text('noUsers', 'No users found. Try another name or email.');
        } catch (error) {
            if (request.signal.aborted) return;
            searchStatus.textContent = text('searchFailed', 'Search failed. Change the search text to try again.');
            const more = results.querySelector('[data-load-more]');
            if (more) more.disabled = false;
        }
    }

    document.getElementById('clearSelectedUser').addEventListener('click', () => {
        userId.value = '';
        userSearch.value = '';
        userSearch.classList.remove('d-none');
        userDisplay.classList.add('d-none');
        userSearch.focus();
    });
    userSearch.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
            event.preventDefault();
            if (!results.classList.contains('d-none')) results.querySelector('button')?.focus();
        }
        if (event.key === 'Escape') results.classList.add('d-none');
    });
    page.addEventListener('keydown', event => {
        if (event.key === 'Escape' && results.contains(event.target)) {
            results.classList.add('d-none');
            userSearch.focus();
        }
    });

    addForm.addEventListener('invalid', () => { addDetails.open = true; }, true);

    // Keep inputs in place on validation, network or session failures. Only a
    // redirect back to this management page confirms a successful operation.
    page.querySelectorAll('form').forEach(form => form.addEventListener('submit', async event => {
        event.preventDefault();
        if (form.dataset.busy === 'true') return;
        if (form === addForm && ((!roleSelect.disabled && !roleSelect.value) ||
            (!userId.disabled && !userId.value))) {
            showError(form, text('invalid', 'Select a user from the search results or choose a role.'));
            (userId.disabled ? roleSelect : userSearch).focus();
            return;
        }
        if (form.classList.contains('remove-share-form') && !window.confirm(text('remove', 'Remove this share? Other access may still apply.'))) return;
        const data = new FormData(form);
        const submit = form.querySelector('[type="submit"]');
        form.dataset.busy = 'true';
        form.setAttribute('aria-busy', 'true');
        submit.disabled = true;
        form.querySelector('.form-error').hidden = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: data });
            const destination = new URL(response.url);
            if (!response.ok || !response.redirected || destination.origin !== location.origin || destination.pathname !== location.pathname) {
                throw new Error('Change not confirmed');
            }
            const error = destination.searchParams.get('error');
            if (error) {
                showError(form, error === 'duplicate'
                    ? text('duplicate', 'This user or role already has access.')
                    : text('invalid', 'Select a user from the search results or choose a role.'));
                return;
            }
            location.assign(destination.href);
        } catch {
            showError(form, text('failed', 'Could not confirm the change. Your input is kept. Try again, or reload to check the saved state.'));
        } finally {
            form.dataset.busy = 'false';
            form.removeAttribute('aria-busy');
            submit.disabled = false;
        }
    }));

    const error = new URLSearchParams(location.search).get('error');
    if (error) showError(addForm, error === 'duplicate'
        ? text('duplicate', 'This user or role already has access.')
        : text('invalid', 'Select a user from the search results or choose a role.'));
})();
