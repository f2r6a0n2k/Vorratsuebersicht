const API = '';
let currentArticles = [];

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
    const search = document.getElementById('article-search')?.value || '';
    const params = search ? '?search=' + encodeURIComponent(search) : '';
    const res = await fetch(API + '/api/articles' + params);
    currentArticles = await res.json();
    renderArticles();
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
                <button class="btn-danger" onclick="deleteArticle(${a.articleId})" title="L&ouml;schen">&times;</button>
            </div>
        </div>
    `).join('');
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

    if (id) {
        await fetch(API + '/api/articles/' + id, { method: 'PUT', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
    } else {
        await fetch(API + '/api/articles', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
    }
    closeModal();
    loadArticles();
    loadStorageItems();
}

async function deleteArticle(id) {
    if (!confirm('Wirklich l\u00f6schen?')) return;
    await fetch(API + '/api/articles/' + id, { method: 'DELETE' });
    loadArticles();
    loadStorageItems();
}

// ===== Storage Items =====

async function loadStorageItems() {
    const res = await fetch(API + '/api/storage-items');
    const items = await res.json();
    renderStorageItems(items);
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
        // Fetch article for expiry warnings
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

    await fetch(API + '/api/storage-items', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
    closeModal();
    loadStorageItems();
}

async function deleteStorageItem(id) {
    if (!confirm('Wirklich l\u00f6schen?')) return;
    await fetch(API + '/api/storage-items/' + id, { method: 'DELETE' });
    loadStorageItems();
}

function editStorageItem(id) {
    // For simplicity, delete and recreate
    deleteStorageItem(id).then(() => showStorageForm());
}

// ===== Shopping Items =====

async function loadShoppingItems() {
    const res = await fetch(API + '/api/shopping-items');
    const items = await res.json();
    renderShoppingItems(items);
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

    await fetch(API + '/api/shopping-items', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(data) });
    closeModal();
    loadShoppingItems();
}

async function toggleShoppingItem(id, checked) {
    const res = await fetch(API + '/api/shopping-items/' + id);
    const item = await res.json();
    item.isChecked = checked;
    await fetch(API + '/api/shopping-items/' + id, { method: 'PUT', headers: {'Content-Type':'application/json'}, body: JSON.stringify(item) });
    loadShoppingItems();
}

async function deleteShoppingItem(id) {
    if (!confirm('Wirklich l\u00f6schen?')) return;
    await fetch(API + '/api/shopping-items/' + id, { method: 'DELETE' });
    loadShoppingItems();
}

// ===== Sync =====

async function loadSyncChanges() {
    const since = prompt('Alle \u00c4nderungen seit (ISO8601, leer f\u00fcr alle):', '');
    const params = since ? '?since=' + encodeURIComponent(since) : '';
    const res = await fetch(API + '/api/sync/changes' + params);
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
}

// ===== Server Info =====

async function loadServerInfo() {
    try {
        const res = await fetch(API + '/api/discovery');
        const info = await res.json();
        document.getElementById('server-info').textContent = info.name + ' (' + info.hostName + ' - ' + (info.localIps || []).join(', ') + ')';
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
