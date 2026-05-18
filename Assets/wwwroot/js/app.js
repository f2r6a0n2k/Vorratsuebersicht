const API = '';
let currentArticles = [];
let currentStorage = [];
let accessKey = localStorage.getItem('sync_access_key') || sessionStorage.getItem('sync_access_key') || '';
let pendingAccessKeyPrompt = null;

function apiFetch(path, options = {}) {
    const doFetch = (key) => {
        const opts = { ...options };
        const headers = { ...(opts.headers || {}) };
        if (key) headers['X-Access-Key'] = key;
        opts.headers = headers;
        return fetch(API + path, opts);
    };
    return doFetch(accessKey).then(async res => {
        if (res.status === 401) {
            const key = await promptAccessKey();
            if (key) {
                accessKey = key;
                return doFetch(key);
            }
            throw new Error('Access denied');
        }
        return res;
    });
}

function promptAccessKey() {
    if (pendingAccessKeyPrompt) return pendingAccessKeyPrompt;
    pendingAccessKeyPrompt = new Promise(resolve => {
        const m = document.getElementById('modal');
        document.getElementById('modal-body').innerHTML = `
            <h2>Zugangsschlüssel (PIN)</h2>
            <p style="color:#e74c3c;margin-bottom:1rem">Zugangsschlüssel erforderlich</p>
            <form onsubmit="event.preventDefault(); submitAccessKey()">
                <div class="form-group">
                    <input id="access-key-input" type="password" placeholder="PIN eingeben" autofocus style="font-size:1.2rem;text-align:center;letter-spacing:0.3em">
                </div>
                <div class="form-group">
                    <label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer">
                        <input id="remember-key" type="checkbox" checked> PIN merken
                    </label>
                </div>
                <div class="form-actions">
                    <button type="button" class="btn-danger" onclick="cancelAccessKey()">Abbrechen</button>
                    <button type="submit" class="btn-success">Bestätigen</button>
                </div>
            </form>
        `;
        m.classList.remove('hidden');
        document.getElementById('access-key-input').focus();
        window.submitAccessKey = function() {
            const key = document.getElementById('access-key-input').value;
            if (key) {
                if (document.getElementById('remember-key').checked)
                    localStorage.setItem('sync_access_key', key);
                else
                    sessionStorage.setItem('sync_access_key', key);
            }
            m.classList.add('hidden');
            pendingAccessKeyPrompt = null;
            resolve(key || null);
        };
        window.cancelAccessKey = function() {
            m.classList.add('hidden');
            pendingAccessKeyPrompt = null;
            resolve(null);
        };
    });
    return pendingAccessKeyPrompt;
}

function saveAccessKey(key) {
    if (!key) return;
    localStorage.setItem('sync_access_key', key);
    accessKey = key;
}

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.tab').forEach(t => {
        t.addEventListener('click', () => {
            document.querySelectorAll('.tab').forEach(x => x.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(x => x.classList.remove('active'));
            t.classList.add('active');
            document.getElementById('tab-' + t.dataset.tab).classList.add('active');
        });
    });
    loadArticles();
    loadStorageItems();
    loadShoppingItems();
    loadServerInfo();
});

function closeModal() {
    document.getElementById('modal').classList.add('hidden');
}

// ===== Articles =====

async function loadArticles() {
    try {
        const search = document.getElementById('article-search')?.value || '';
        const params = search ? '?search=' + encodeURIComponent(search) : '';
        const res = await apiFetch('/api/articles' + params);
        currentArticles = await res.json();
        renderArticles();
    } catch (e) {
        console.error('loadArticles:', e);
    }
}

function renderArticles() {
    const el = document.getElementById('article-list');
    el.innerHTML = currentArticles.map(a => `
        <div class="data-item">
            <div class="item-main">
                <div class="item-name">${esc(a.name)}</div>
                <div class="item-detail">
                    ${[a.manufacturer, a.category, a.subCategory].filter(Boolean).join(' &raquo; ')}
                    ${a.eanCode ? '&middot; EAN: ' + esc(a.eanCode) : ''}
                    ${a.storageName ? '&middot; Ort: ' + esc(a.storageName) : ''}
                </div>
            </div>
            <div class="item-actions">
                <button onclick="showArticleForm(${a.articleId})" title="Bearbeiten">&#9998;</button>
                <button onclick="showAddToStorage(${a.articleId}, '${escAttr(a.name)}')" title="Ins Lager">&#128230;</button>
                <button onclick="showAddToShopping(${a.articleId}, '${escAttr(a.name)}')" title="Einkaufen">&#128722;</button>
                <button class="btn-danger" onclick="deleteArticle(${a.articleId})" title="L&ouml;schen">&times;</button>
            </div>
        </div>
    `).join('');
}

function showAddToStorage(articleId, articleName) {
    document.getElementById('modal-body').innerHTML = `
        <h2>Ins Lager aufnehmen</h2>
        <p><strong>${esc(articleName)}</strong></p>
        <form onsubmit="addToStorage(event, ${articleId})">
            <div class="form-row">
                <div class="form-group"><label>Menge *</label><input name="quantity" type="number" value="1" required></div>
                <div class="form-group"><label>MHD</label><input name="bestBeforeDate" type="date"></div>
            </div>
            <div class="form-group"><label>Lagername</label><input name="storageName" value=""></div>
            <div class="form-actions">
                <button type="button" class="btn-danger" onclick="closeModal()">Abbrechen</button>
                <button type="submit" class="btn-success">Ins Lager</button>
            </div>
        </form>
    `;
    document.getElementById('modal').classList.remove('hidden');
}

async function addToStorage(event, articleId) {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    data.articleId = articleId;
    data.quantity = parseInt(data.quantity) || 1;
    data.bestBeforeDate = data.bestBeforeDate || null;
    data.storageName = data.storageName || null;
    try {
        await apiFetch('/api/storage-items', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
        closeModal();
        loadStorageItems();
    } catch (e) {
        alert('Fehler: ' + e.message);
    }
}

function showAddToShopping(articleId, articleName) {
    document.getElementById('modal-body').innerHTML = `
        <h2>Auf Einkaufsliste setzen</h2>
        <p><strong>${esc(articleName)}</strong></p>
        <form onsubmit="addToShopping(event, ${articleId})">
            <div class="form-group"><label>Menge *</label><input name="quantity" type="number" value="1" required></div>
            <div class="form-actions">
                <button type="button" class="btn-danger" onclick="closeModal()">Abbrechen</button>
                <button type="submit" class="btn-success">Auf Einkaufsliste</button>
            </div>
        </form>
    `;
    document.getElementById('modal').classList.remove('hidden');
}

async function addToShopping(event, articleId) {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    data.articleId = articleId;
    data.quantity = parseInt(data.quantity) || 1;
    try {
        await apiFetch('/api/shopping-items', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
        closeModal();
        loadShoppingItems();
    } catch (e) {
        alert('Fehler: ' + e.message);
    }
}

function showArticleForm(id) {
    const article = id ? currentArticles.find(a => a.articleId === id) : null;
    document.getElementById('modal-body').innerHTML = `
        <h2>${article ? 'Artikel bearbeiten' : 'Neuer Artikel'}</h2>
        <form onsubmit="saveArticle(event, ${id || ''})">
            <div class="form-group">
                <label>Name *</label>
                <input name="name" value="${escAttr(article?.name || '')}" required>
            </div>
            <div class="form-row">
                <div class="form-group"><label>Hersteller</label><input name="manufacturer" value="${escAttr(article?.manufacturer || '')}"></div>
                <div class="form-group"><label>Kategorie</label><input name="category" value="${escAttr(article?.category || '')}"></div>
            </div>
            <div class="form-row">
                <div class="form-group"><label>Unterkategorie</label><input name="subCategory" value="${escAttr(article?.subCategory || '')}"></div>
                <div class="form-group"><label>Lagername</label><input name="storageName" value="${escAttr(article?.storageName || '')}"></div>
            </div>
            <div class="form-row">
                <div class="form-group"><label>Gr&ouml;&szlig;e</label><input name="size" type="number" step="0.01" value="${article?.size ?? ''}"></div>
                <div class="form-group"><label>Einheit</label><input name="unit" value="${escAttr(article?.unit || '')}"></div>
            </div>
            <div class="form-row">
                <div class="form-group"><label>EAN-Code</label><input name="eanCode" value="${escAttr(article?.eanCode || '')}"></div>
                <div class="form-group"><label>Supermarkt</label><input name="supermarket" value="${escAttr(article?.supermarket || '')}"></div>
            </div>
            <div class="form-row">
                <div class="form-group"><label>Preis</label><input name="price" type="number" step="0.01" value="${article?.price ?? ''}"></div>
                <div class="form-group"><label>Kalorien</label><input name="calorie" type="number" value="${article?.calorie ?? ''}"></div>
            </div>
            <div class="form-row">
                <div class="form-group"><label>Mindestmenge</label><input name="minQuantity" type="number" value="${article?.minQuantity ?? ''}"></div>
                <div class="form-group"><label>Vorzugsmenge</label><input name="prefQuantity" type="number" value="${article?.prefQuantity ?? ''}"></div>
            </div>
            <div class="form-row">
                <div class="form-group">
                    <label>Warnung in Tagen</label>
                    <input name="warnInDays" type="number" value="${article?.warnInDays ?? ''}">
                </div>
                <div class="form-group" style="display:flex;align-items:center;gap:0.5rem;padding-top:1.5rem">
                    <input name="durableInfinity" type="checkbox" ${article?.durableInfinity ? 'checked' : ''} id="durable">
                    <label for="durable">Unbegrenzt haltbar</label>
                </div>
            </div>
            <div class="form-group"><label>Notizen</label><textarea name="notes">${esc(article?.notes || '')}</textarea></div>
            <div class="form-actions">
                <button type="button" class="btn-danger" onclick="closeModal()">Abbrechen</button>
                <button type="submit" class="btn-success">Speichern</button>
            </div>
        </form>
    `;
    document.getElementById('modal').classList.remove('hidden');
}

async function saveArticle(event, id) {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    data.durableInfinity = !!data.durableInfinity;
    data.size = data.size ? parseFloat(data.size) : null;
    data.price = data.price ? parseFloat(data.price) : null;
    data.calorie = data.calorie ? parseInt(data.calorie) : null;
    data.minQuantity = data.minQuantity ? parseInt(data.minQuantity) : null;
    data.prefQuantity = data.prefQuantity ? parseInt(data.prefQuantity) : null;
    data.warnInDays = data.warnInDays ? parseInt(data.warnInDays) : null;

    try {
        if (id) {
            await apiFetch('/api/articles/' + id, { method: 'PUT', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
        } else {
            await apiFetch('/api/articles', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
        }
        closeModal();
        loadArticles();
        loadStorageItems();
    } catch (e) {
        alert('Fehler beim Speichern: ' + e.message);
    }
}

async function deleteArticle(id) {
    if (!confirm('Wirklich l\u00f6schen?')) return;
    try {
        await apiFetch('/api/articles/' + id, { method: 'DELETE' });
        loadArticles();
        loadStorageItems();
    } catch (e) {
        alert('Fehler beim L\u00f6schen: ' + e.message);
    }
}

// ===== Storage Items =====

async function loadStorageItems() {
    try {
        const res = await apiFetch('/api/storage-items');
        currentStorage = await res.json();
        filterStorage();
    } catch (e) {
        console.error('loadStorageItems:', e);
    }
}

function filterStorage() {
    const search = (document.getElementById('storage-search')?.value || '').toLowerCase();
    if (!search) return renderStorageItems(currentStorage);
    renderStorageItems(currentStorage.filter(s =>
        (s.articleName || '').toLowerCase().includes(search) ||
        (s.storageName || '').toLowerCase().includes(search)
    ));
}

function renderStorageItems(items) {
    const el = document.getElementById('storage-list');
    const now = new Date();
    el.innerHTML = items.map(s => {
        const bestBefore = s.bestBeforeDate ? new Date(s.bestBeforeDate) : null;
        let cls = '';
        if (bestBefore) {
            const diff = (bestBefore - now) / (1000 * 60 * 60 * 24);
            if (diff < 0) cls = 'expired';
            else if (diff < 7) cls = 'warning';
        }
        return `
            <div class="data-item ${cls}">
                <div class="item-main">
                    <div class="item-name">${esc(s.articleName || 'Artikel #' + s.articleId)}</div>
                    <div class="item-detail">
                        Menge: ${s.quantity}
                        ${s.bestBeforeDate ? '&middot; MHD: ' + new Date(s.bestBeforeDate).toLocaleDateString('de-DE') : ''}
                        ${s.storageName ? '&middot; Ort: ' + esc(s.storageName) : ''}
                    </div>
                </div>
                <div class="item-actions">
                    <button onclick="editStorageItem(${s.storageItemId})" title="Bearbeiten">&#9998;</button>
                    <button class="btn-danger" onclick="deleteStorageItem(${s.storageItemId})" title="L&ouml;schen">&times;</button>
                </div>
            </div>
        `;
    }).join('');
}

function showStorageForm() {
    document.getElementById('modal-body').innerHTML = `
        <h2>Neuer Lagerzugang</h2>
        <form onsubmit="saveStorageItem(event)">
            <div class="form-group">
                <label>Artikel *</label>
                <select name="articleId" required>
                    <option value="">Bitte w&auml;hlen...</option>
                    ${currentArticles.map(a => `<option value="${a.articleId}">${esc(a.name)}</option>`).join('')}
                </select>
            </div>
            <div class="form-row">
                <div class="form-group"><label>Menge *</label><input name="quantity" type="number" value="1" required></div>
                <div class="form-group"><label>MHD</label><input name="bestBeforeDate" type="date"></div>
            </div>
            <div class="form-group"><label>Lagername</label><input name="storageName" value=""></div>
            <div class="form-actions">
                <button type="button" class="btn-danger" onclick="closeModal()">Abbrechen</button>
                <button type="submit" class="btn-success">Speichern</button>
            </div>
        </form>
    `;
    document.getElementById('modal').classList.remove('hidden');
}

async function saveStorageItem(event) {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    data.articleId = parseInt(data.articleId);
    data.quantity = parseInt(data.quantity);
    data.bestBeforeDate = data.bestBeforeDate || null;
    data.storageName = data.storageName || null;

    try {
        await apiFetch('/api/storage-items', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
        closeModal();
        loadStorageItems();
    } catch (e) {
        alert('Fehler beim Speichern: ' + e.message);
    }
}

async function deleteStorageItem(id) {
    if (!confirm('Wirklich l\u00f6schen?')) return;
    try {
        await apiFetch('/api/storage-items/' + id, { method: 'DELETE' });
        loadStorageItems();
    } catch (e) {
        alert('Fehler beim L\u00f6schen: ' + e.message);
    }
}

function editStorageItem(id) {
    const item = currentStorage.find(s => s.storageItemId === id);
    if (!item) return;
    const mhd = item.bestBeforeDate ? item.bestBeforeDate.substring(0, 10) : '';
    document.getElementById('modal-body').innerHTML = `
        <h2>Lagerposition bearbeiten</h2>
        <p><strong>${esc(item.articleName || 'Artikel #' + item.articleId)}</strong></p>
        <form onsubmit="saveStorageEdit(event, ${id})">
            <div class="form-row">
                <div class="form-group"><label>Menge *</label><input name="quantity" type="number" value="${item.quantity}" required></div>
                <div class="form-group"><label>MHD</label><input name="bestBeforeDate" type="date" value="${mhd}"></div>
            </div>
            <div class="form-group"><label>Lagername</label><input name="storageName" value="${escAttr(item.storageName || '')}"></div>
            <div class="form-actions">
                <button type="button" class="btn-danger" onclick="closeModal()">Abbrechen</button>
                <button type="submit" class="btn-success">Speichern</button>
            </div>
        </form>
    `;
    document.getElementById('modal').classList.remove('hidden');
}

async function saveStorageEdit(event, id) {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    const changes = [{
        clientChangeId: crypto.randomUUID?.() || Math.random().toString(36).substring(2),
        entityType: 'StorageItem',
        operation: 'update',
        entityId: id,
        data: {
            quantity: parseInt(data.quantity) || 1,
            bestBeforeDate: data.bestBeforeDate || null,
            storageName: data.storageName || null
        }
    }];
    try {
        await apiFetch('/api/sync/push', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(changes) });
        closeModal();
        loadStorageItems();
    } catch (e) {
        alert('Fehler: ' + e.message);
    }
}

// ===== Shopping Items =====

async function loadShoppingItems() {
    try {
        const res = await apiFetch('/api/shopping-items');
        const items = await res.json();
        renderShoppingItems(items);
    } catch (e) {
        console.error('loadShoppingItems:', e);
    }
}

function renderShoppingItems(items) {
    const el = document.getElementById('shopping-list');
    el.innerHTML = items.map(s => `
        <div class="data-item ${s.isChecked ? 'checked' : ''}">
            <div class="item-main">
                <div class="item-name">${esc(s.articleName || 'Artikel #' + s.articleId)}</div>
                <div class="item-detail">Menge: ${s.quantity}</div>
            </div>
            <div class="item-actions">
                <button class="${s.isChecked ? 'btn-warning' : 'btn-success'}" onclick="toggleShoppingItem(${s.shoppingItemId}, ${!s.isChecked})">
                    ${s.isChecked ? 'R&uuml;ckg&auml;ngig' : 'Erledigt'}
                </button>
                <button class="btn-danger" onclick="deleteShoppingItem(${s.shoppingItemId})">&times;</button>
            </div>
        </div>
    `).join('');
}

function showShoppingForm() {
    document.getElementById('modal-body').innerHTML = `
        <h2>Neuer Einkaufszettel-Eintrag</h2>
        <form onsubmit="saveShoppingItem(event)">
            <div class="form-group">
                <label>Artikel *</label>
                <select name="articleId" required onchange="updateArticleName(this)">
                    <option value="">Bitte w&auml;hlen...</option>
                    ${currentArticles.map(a => `<option value="${a.articleId}">${esc(a.name)}</option>`).join('')}
                </select>
            </div>
            <div class="form-group"><label>Menge</label><input name="quantity" type="number" value="1"></div>
            <div class="form-actions">
                <button type="button" class="btn-danger" onclick="closeModal()">Abbrechen</button>
                <button type="submit" class="btn-success">Speichern</button>
            </div>
        </form>
    `;
    document.getElementById('modal').classList.remove('hidden');
}

function updateArticleName(select) {
    const article = currentArticles.find(a => a.articleId === parseInt(select.value));
    if (article && !document.querySelector('[name="articleName"]')) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = 'articleName';
        input.value = article.name;
        select.form.appendChild(input);
    }
}

async function saveShoppingItem(event) {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    data.articleId = parseInt(data.articleId);
    data.quantity = parseInt(data.quantity);
    const article = currentArticles.find(a => a.articleId === data.articleId);
    data.articleName = article?.name || null;

    try {
        await apiFetch('/api/shopping-items', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
        closeModal();
        loadShoppingItems();
    } catch (e) {
        alert('Fehler beim Speichern: ' + e.message);
    }
}

async function toggleShoppingItem(id, checked) {
    try {
        await apiFetch('/api/shopping-items/' + id, { method: 'PUT', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ isChecked: checked }) });
        loadShoppingItems();
    } catch (e) {
        alert('Fehler: ' + e.message);
    }
}

async function deleteShoppingItem(id) {
    if (!confirm('Wirklich l\u00f6schen?')) return;
    try {
        await apiFetch('/api/shopping-items/' + id, { method: 'DELETE' });
        loadShoppingItems();
    } catch (e) {
        alert('Fehler beim L\u00f6schen: ' + e.message);
    }
}

// ===== Sync =====

async function loadSyncChanges() {
    try {
        const since = prompt('Alle \u00c4nderungen seit (ISO8601, leer f\u00fcr alle):', '');
        if (since === null) return;
        const params = since ? '?since=' + encodeURIComponent(since) : '';
        const res = await apiFetch('/api/sync/changes' + params);
        const changes = await res.json();
        const el = document.getElementById('sync-changes');
        document.getElementById('sync-info').textContent = changes.length + ' \u00c4nderungen gefunden.';
        el.innerHTML = changes.map(c => `
            <div class="data-item">
                <div class="item-main">
                    <div class="item-name">${esc(c.entityType)} #${c.entityId} &mdash; ${esc(c.operation)}</div>
                    <div class="item-detail">${new Date(c.timestamp).toLocaleString('de-DE')}</div>
                </div>
            </div>
        `).join('');
    } catch (e) {
        console.error('loadSyncChanges:', e);
    }
}

// ===== Server Info =====

async function loadServerInfo() {
    try {
        const res = await apiFetch('/api/db/info');
        const info = await res.json();
        const ver = 'Vorratsübersicht SyncServer';
        document.getElementById('server-info').textContent = ver + ' (DB: ' + (info.databaseId || '?') + ')';
    } catch {
        document.getElementById('server-info').textContent = 'nicht erreichbar';
    }
}

// ===== Helpers =====

function esc(s) {
    if (!s) return '';
    const div = document.createElement('div');
    div.textContent = s;
    return div.innerHTML;
}

function escAttr(s) {
    if (!s) return '';
    return s.replace(/&/g,'&amp;').replace(/"/g,'&quot;').replace(/'/g,'&#39;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}
