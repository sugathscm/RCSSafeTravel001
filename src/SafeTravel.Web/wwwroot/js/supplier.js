/* Admin Portal (Supplier): a business keeps its listing up to date, uploads documents and pays the annual fee.
   Everything is read from and sent to /api/v1/supplier with AJAX. */
(function ($) {
  'use strict';
  var e = ST.esc, lkr = ST.lkr, $main = $('#main');
  var me = null, fee = 0, reminderDays = [30, 14, 7, 0], cats = [], current = 'overview';
  var DISTRICTS = ['Ampara', 'Anuradhapura', 'Badulla', 'Batticaloa', 'Colombo', 'Galle', 'Gampaha', 'Hambantota', 'Jaffna', 'Kalutara', 'Kandy', 'Kegalle', 'Kilinochchi', 'Kurunegala', 'Mannar', 'Matale', 'Matara', 'Monaragala', 'Mullaitivu', 'Nuwara Eliya', 'Polonnaruwa', 'Puttalam', 'Ratnapura', 'Trincomalee', 'Vavuniya'];

  function head(t, sub) { return '<h2>' + t + '</h2><p class="sub">' + sub + '</p>'; }
  function field(id, label, input) { return '<div class="field"><label class="form-label" for="' + id + '">' + label + '</label>' + input + '</div>'; }
  function text(id, name, value, attrs) { return '<input class="form-control" id="' + id + '" name="' + name + '" value="' + e(value) + '" ' + (attrs || '') + '>'; }
  function minus(iso, n) { var d = new Date(iso + 'T00:00:00'); d.setDate(d.getDate() - n); return d; }
  function left() { return me.daysLeft == null ? '-' : me.daysLeft < 0 ? 'Lapsed' : me.daysLeft + ' days'; }

  /** Where the business is on the road to going live: -1 rejected, 1 in review, 3 must pay, 5 live. */
  function stage() {
    var l = me.listing;
    if (me.status === 'rejected') return -1;
    if (me.status === 'pending') return 1;
    if (!me.paidUntil || (l && (l.status === 'unpaid' || l.status === 'lapsed'))) return 3;
    return 5;
  }

  var screens = {};

  screens.overview = function () {
    var n = stage(), l = me.listing;
    var steps = ['Registered', 'Documents checked', 'Approved', 'Annual fee paid', 'Live on the map'];
    var msg = n === -1 ? ['bad', 'Not approved', 'Your registration was not approved.' + (me.rejectReason ? ' Reason: ' + e(me.rejectReason) : '') + ' Check your documents and contact the support team.'] :
      n === 1 ? ['warn', 'In review', 'Your registration is with the admin team. ' + (me.documents.length ? 'Most reviews take two working days.' : 'Upload your documents so they can check your business.')] :
        n === 3 ? ['info', 'Action needed', l && l.status === 'lapsed' ? 'Your listing is hidden because the annual fee has lapsed. Pay to bring it back.' : 'You are approved. Pay the annual fee to go live.'] :
          l && l.status === 'hidden' ? ['warn', 'Hidden', 'The admin team has hidden your listing. Contact the support team.'] : ['ok', 'Live', 'Your listing is live on the web portal and in the mobile app.'];
    return head(e(me.businessName), 'Your listing status, results and renewal date.') +
      '<div class="panel"><div class="body hrow"><span class="pill ' + msg[0] + '">' + msg[1] + '</span><span>' + msg[2] + '</span><span class="sp"></span>' +
      (n === 3 ? '<a class="btn btn-danger" href="#payment">Pay ' + lkr(fee) + '</a>' : n === 1 && !me.documents.length ? '<a class="btn btn-dark" href="#documents">Upload documents</a>' : '') + '</div></div>' +
      '<div class="steps">' + steps.map(function (t, i) { return '<div class="step ' + (i < n ? 'done' : i === n ? 'now' : '') + '"><span class="small mute">Step ' + (i + 1) + '</span><b>' + (i < n ? '✓ ' : '') + t + '</b></div>'; }).join('') + '</div>' +
      '<div class="stats"><div class="stat"><b>' + me.views.toLocaleString() + '</b><span>Listing views</span></div><div class="stat"><b>' + me.calls.toLocaleString() + '</b><span>Calls from the portal and app</span></div><div class="stat"><b>' + me.directions.toLocaleString() + '</b><span>Direction requests</span></div>' +
      '<div class="stat"><b>' + (l && l.rating ? '★ ' + l.rating.toFixed(1) : '-') + '</b><span>' + (l && l.reviews ? l.reviews + ' traveller reviews' : 'No reviews yet') + '</span></div>' +
      '<div class="stat"><b>' + left() + '</b><span>' + (me.paidUntil ? 'Until renewal on ' + ST.date(me.paidUntil) : 'Not paid yet') + '</span></div></div>';
  };

  var LSTATUS = { published: ['ok', 'Live'], pending: ['warn', 'Awaiting review'], unpaid: ['info', 'Awaiting payment'], lapsed: ['bad', 'Lapsed'], hidden: ['', 'Hidden'], rejected: ['bad', 'Rejected'] };
  var editing = null; // null: list only, 0: new listing, otherwise the id being edited
  var productsOf = null, products = [], productEdit = null; // the listing whose products are open, its products, and the one being edited (0: new)

  function productForm(p) {
    var isNew = !p; p = p || { id: 0, name: '', description: '', price: '', unit: '', isActive: true };
    return '<form class="panel" data-form="product" data-id="' + p.id + '"><header><h3>' + (isNew ? 'Add a product' : 'Edit ' + e(p.name)) + '</h3></header><div class="body"><div class="grid2">' +
      field('sp-name', 'Name', text('sp-name', 'name', p.name, 'required minlength="2" maxlength="120" placeholder="Deluxe double room"')) +
      field('sp-file', 'Picture (JPG or PNG, up to 2 MB)', '<input class="form-control" id="sp-file" type="file" accept=".jpg,.jpeg,.png">') +
      field('sp-price', 'Price in LKR (leave empty for "Ask for price")', text('sp-price', 'price', p.price == null ? '' : p.price, 'type="number" min="0" step="0.01"')) +
      field('sp-unit', 'Price is for', text('sp-unit', 'unit', p.unit, 'maxlength="40" placeholder="per night"')) + '</div>' +
      '<div class="mt-3">' + field('sp-desc', 'Description', '<textarea class="form-control" id="sp-desc" name="description" rows="2" maxlength="400">' + e(p.description) + '</textarea>') + '</div>' +
      '<div class="form-check form-switch mt-3 mb-0"><input class="form-check-input" type="checkbox" role="switch" id="sp-on" name="isActive"' + (p.isActive ? ' checked' : '') + '><label class="form-check-label fw-semibold" for="sp-on">Show to travellers</label></div>' +
      '<div class="hrow mt-3"><button class="btn btn-danger" type="submit">' + (isNew ? 'Add product' : 'Save product') + '</button><button type="button" class="btn btn-plain" data-act="productCancel">Cancel</button></div></div></form>';
  }

  function productsScreen() {
    var l = (me.listings || []).filter(function (x) { return x.id === productsOf; })[0];
    var x = productEdit ? products.filter(function (p) { return p.id === productEdit; })[0] : null;
    return head('Products of ' + e(l.name), 'Rooms, dishes, tours, rentals: anything you sell under this listing. Travellers see them with prices when they open your tile.') +
      '<div class="hrow mt-3"><button type="button" class="btn btn-plain" data-act="productsBack">← All listings</button>' + (productEdit === null ? '<button type="button" class="btn btn-danger" data-act="productAdd">+ Add a product</button>' : '') + '</div>' +
      (productEdit === 0 ? productForm(null) : x ? productForm(x) : '') +
      '<section class="panel"><div class="tw"><table class="t"><thead><tr><th scope="col">Product</th><th scope="col">Price</th><th scope="col">Shown</th><th scope="col"></th></tr></thead><tbody>' +
      (products.map(function (p) {
        return '<tr><td><div class="hrow flex-nowrap">' + (p.imageUrl ? '<img class="pthumb" src="' + e(p.imageUrl) + '" alt="">' : '<span class="pthumb" aria-hidden="true">🛍️</span>') + '<div><b>' + e(p.name) + '</b><span class="small mute">' + e(p.description) + '</span></div></div></td>' +
          '<td class="num text-nowrap">' + (p.price != null ? lkr(p.price) + (p.unit ? '<span class="small mute">' + e(p.unit) + '</span>' : '') : '<span class="mute small">Ask for price</span>') + '</td>' +
          '<td><span class="pill ' + (p.isActive ? 'ok">Yes' : '">No') + '</span></td>' +
          '<td><div class="acts"><button type="button" class="btn btn-sm btn-plain" data-act="productEdit" data-id="' + p.id + '">Edit</button><button type="button" class="btn btn-sm btn-plain" data-act="productRemove" data-id="' + p.id + '">Remove</button></div></td></tr>';
      }).join('') || '<tr><td colspan="4" class="mute">No products yet. Add your first one.</td></tr>') + '</tbody></table></div></section>';
  }
  function openProducts(id) {
    return ST.api('GET', 'supplier/me/listings/' + id + '/products').done(function (list) { productsOf = Number(id); products = list; productEdit = null; show(); window.scrollTo(0, 0); });
  }

  function listingForm(l) {
    var isNew = !l; l = l || { id: 0, name: '', categoryId: 0, town: '', district: (me.listing || {}).district || 'Colombo', phone: me.phone || '', hours: '', description: '', latitude: '', longitude: '' };
    return '<form class="panel" data-form="listing" data-id="' + l.id + '"><header><h3>' + (isNew ? 'Add a listing' : 'Edit ' + e(l.name)) + '</h3></header><div class="body"><div class="grid2">' +
      field('sl-name', 'Name shown to travellers', text('sl-name', 'name', l.name, 'required maxlength="160" placeholder="Ella Ridge Restaurant"')) +
      field('sl-cat', 'Category', '<select class="form-select" id="sl-cat" name="categoryId">' + cats.filter(function (c) { return c.kind === 'business'; }).map(function (c) { return '<option value="' + c.id + '"' + (c.id === l.categoryId ? ' selected' : '') + '>' + e(c.icon) + ' ' + e(c.name) + '</option>'; }).join('') + '</select>') +
      field('sl-town', 'Town', text('sl-town', 'town', l.town, 'required maxlength="80"')) +
      field('sl-district', 'District', '<select class="form-select" id="sl-district" name="district">' + DISTRICTS.map(function (d) { return '<option' + (d === l.district ? ' selected' : '') + '>' + d + '</option>'; }).join('') + '</select>') +
      field('sl-phone', 'Phone', text('sl-phone', 'phone', l.phone, 'required maxlength="30" inputmode="tel"')) +
      field('sl-hours', 'Opening hours', text('sl-hours', 'hours', l.hours, 'maxlength="160"')) +
      field('sl-lat', 'Latitude (optional)', text('sl-lat', 'latitude', l.latitude || '', 'type="number" step="any" min="5.5" max="10.2"')) +
      field('sl-lng', 'Longitude (optional)', text('sl-lng', 'longitude', l.longitude || '', 'type="number" step="any" min="79" max="82.5"')) + '</div>' +
      '<div class="mt-3">' + field('sl-desc', 'Description and safety arrangements', '<textarea class="form-control" id="sl-desc" name="description" rows="3" maxlength="1000">' + e(l.description) + '</textarea>') + '</div>' +
      '<div class="hrow mt-3"><button class="btn btn-danger" type="submit">' + (isNew ? 'Add listing' : 'Save listing') + '</button><button type="button" class="btn btn-plain" data-act="editCancel">Cancel</button></div></div></form>';
  }

  screens.listing = function () {
    if (productsOf) return productsScreen();
    var list = me.listings || [], first = list.length ? list[0].id : 0;
    var x = editing ? list.filter(function (l) { return l.id === editing; })[0] : null;
    var canAdd = me.status !== 'rejected';
    return head('My listings', 'Everything you offer: your stay, your restaurant, your rooms, your tours. Each listing has its own tile on the web portal and in the app, and can carry a promotion.') +
      (editing === 0 ? listingForm(null) : x ? listingForm(x) : canAdd ? '<div class="hrow mt-3"><button type="button" class="btn btn-danger" data-act="add">+ Add a listing</button><span class="small mute">' +
        (me.status === 'pending' ? 'New listings go live after the admin team approves your registration.' : me.paidUntil && me.daysLeft >= 0 ? 'New listings go live at once. Your annual fee covers them.' : 'New listings go live once the annual fee is paid.') + '</span></div>' : '') +
      '<section class="panel"><div class="tw"><table class="t"><thead><tr><th scope="col">Listing</th><th scope="col">Status</th><th scope="col">Promotion</th><th scope="col"></th></tr></thead><tbody>' +
      list.map(function (l) {
        var s = LSTATUS[l.status] || ['', l.status], p = l.promotion;
        return '<tr><td><b>' + e(l.icon) + ' ' + e(l.name) + '</b><span class="small mute">' + e(l.category) + ' · ' + e(l.town) + '</span></td><td><span class="pill ' + s[0] + '">' + s[1] + '</span></td>' +
          '<td>' + (p ? '<span class="pill bad">Promotion</span> ' + e(p.title) + '<span class="small mute">' + (p.until ? 'Until ' + ST.date(p.until) : 'No end date') + '</span>' : '<span class="mute small">None</span>') + '</td>' +
          '<td><div class="acts"><button type="button" class="btn btn-sm ' + (p ? 'btn-plain' : 'btn-dark') + '" data-act="promo" data-id="' + l.id + '">' + (p ? 'Change promotion' : 'Add promotion') + '</button>' +
          '<button type="button" class="btn btn-sm btn-plain" data-act="products" data-id="' + l.id + '">Products</button><button type="button" class="btn btn-sm btn-plain" data-act="edit" data-id="' + l.id + '">Edit</button><button type="button" class="btn btn-sm btn-plain" data-act="preview" data-id="' + l.id + '">Preview</button>' +
          (l.id !== first ? '<button type="button" class="btn btn-sm btn-plain" data-act="remove" data-id="' + l.id + '">Remove</button>' : '') + '</div></td></tr>';
      }).join('') + '</tbody></table></div></section>';
  };

  screens.documents = function () {
    return head('Documents', 'Upload the documents the admin team needs to verify your business. PDF, JPG or PNG, up to 5 MB each.') +
      '<section class="panel"><div class="tw"><table class="t"><thead><tr><th scope="col">Document</th><th scope="col">File</th><th scope="col">Uploaded</th><th scope="col">Status</th></tr></thead><tbody>' +
      (me.documents.map(function (d) { return '<tr><td><b>' + e(d.name) + '</b></td><td><a href="/api/v1/documents/' + d.id + '">' + e(d.fileName) + '</a></td><td class="num text-nowrap">' + ST.dateTime(d.uploadedUtc) + '</td><td><span class="pill ' + (d.status === 'verified' ? 'ok">Verified' : d.status === 'rejected' ? 'bad">Not accepted' : 'warn">Uploaded, waiting for a check') + '</span></td></tr>'; }).join('') || '<tr><td colspan="4" class="mute">No documents yet.</td></tr>') +
      '</tbody></table></div><form data-form="doc" class="body hrow align-items-end" style="border-top:1px solid var(--line)">' +
      '<div class="field" style="flex:1 1 220px"><label class="form-label" for="sd-name">Document type</label><select class="form-select" id="sd-name" name="name"><option>Tourist board licence</option><option>Business registration</option><option>Insurance certificate</option><option>Food safety certificate</option><option>Guide licence</option><option>Owner identity document</option></select></div>' +
      '<div class="field" style="flex:2 1 260px"><label class="form-label" for="sd-file">File</label><input class="form-control" id="sd-file" name="file" type="file" accept=".pdf,.jpg,.jpeg,.png" required></div>' +
      '<button class="btn btn-dark" type="submit">Upload document</button></form></section>';
  };

  screens.payment = function (payments) {
    var canPay = me.status === 'approved';
    return head('Payment and renewal', 'One annual fee keeps your listing on the map.') +
      '<div class="stats"><div class="stat"><b>' + lkr(fee) + '</b><span>Annual fee</span></div><div class="stat"><b>' + (me.paidUntil ? ST.date(me.paidUntil) : '-') + '</b><span>Paid until</span></div><div class="stat"><b>' + left() + '</b><span>Until renewal</span></div></div>' +
      '<div class="panel"><div class="body hrow">' + (canPay ? '<button type="button" class="btn btn-danger" data-act="pay">' + (me.paidUntil ? 'Renew for one year' : 'Pay and go live') + ' · ' + lkr(fee) + '</button><span class="small mute">' + (me.paidUntil && me.daysLeft > 0 ? 'Renewing early adds a year to your current end date.' : 'Your listing goes live as soon as the payment is confirmed.') + '</span>' :
        '<span class="pill warn">Payment opens after approval</span><span>' + (me.status === 'rejected' ? 'Your registration was not approved.' : 'The admin team is still checking your registration.') + '</span>') + '</div></div>' +
      (me.paidUntil ? '<section class="panel"><header><h3>Your renewal reminders</h3></header><div class="tw"><table class="t"><tbody>' + reminderDays.map(function (n) { return '<tr><td class="num text-nowrap">' + ST.date(minus(me.paidUntil, n)) + '</td><td>' + (n ? n + ' days before' : 'Renewal day') + '</td><td>Email and SMS</td></tr>'; }).join('') + '</tbody></table></div></section>' : '') +
      '<section class="panel"><header><h3>Payment history</h3></header><div class="tw"><table class="t"><thead><tr><th scope="col">No.</th><th scope="col">Date</th><th scope="col">Amount</th><th scope="col">Status</th><th scope="col">Reference</th></tr></thead><tbody>' +
      ((payments || []).map(function (p) { return '<tr><td class="num">' + p.id + '</td><td class="num text-nowrap">' + ST.dateTime(p.createdUtc) + '</td><td class="num text-nowrap">' + lkr(p.amount) + '</td><td><span class="pill ' + (p.status === 'Paid' ? 'ok' : 'bad') + '">' + e(p.status) + '</span></td><td class="mono">' + e(p.reference || '') + '</td></tr>'; }).join('') || '<tr><td colspan="5" class="mute">No payments yet.</td></tr>') + '</tbody></table></div></section>';
  };

  // ----- menu and routing -----
  var pages = [['overview', '🏠', 'Overview'], ['listing', '📝', 'My listings'], ['documents', '📄', 'Documents'], ['payment', '💳', 'Payment and renewal']];
  function menu() {
    $('#side-links').html(pages.map(function (p) { return '<a class="link" href="#' + p[0] + '"' + (p[0] === current ? ' aria-current="page"' : '') + '><span>' + p[1] + '</span>' + p[2] + '</a>'; }).join(''));
  }
  function load() {
    return ST.api('GET', 'supplier/me').then(function (r) { me = r.supplier; fee = r.annualFee; reminderDays = r.reminderDays || reminderDays; });
  }
  function show() {
    menu();
    if (current === 'payment') return ST.api('GET', 'supplier/me/payments').done(function (p) { $main.html(screens.payment(p)); });
    $main.html(screens[current]());
    return $.Deferred().resolve().promise();
  }
  function route() {
    var h = location.hash.replace('#', '');
    current = screens[h] ? h : 'overview'; editing = null; productsOf = null;
    load().done(function () { show(); window.scrollTo(0, 0); });
  }
  $(window).on('hashchange', route);

  // ----- actions -----
  function mine(id) { return (me.listings || []).filter(function (l) { return l.id === Number(id); })[0]; }
  function saved(s, msg) { me = s; editing = null; ST.closeModal(); ST.toast(msg); show(); }
  var acts = {
    add: function () { editing = 0; show(); $('#sl-name').trigger('focus'); },
    edit: function (id) { editing = Number(id); show(); window.scrollTo(0, 0); $('#sl-name').trigger('focus'); },
    editCancel: function () { editing = null; show(); },
    remove: function (id) {
      var l = mine(id); if (!l) return;
      ST.modal('<h3>Remove this listing?</h3><p>' + e(l.name) + ' will disappear from the web portal and the app. This cannot be undone.</p><div class="hrow"><span class="sp"></span><button type="button" class="btn btn-plain" data-close>Keep it</button><button type="button" class="btn btn-danger" data-act="removeYes" data-id="' + l.id + '">Remove</button></div>');
    },
    products: function (id) { openProducts(id); },
    productsBack: function () { productsOf = null; show(); },
    productAdd: function () { productEdit = 0; show(); $('#sp-name').trigger('focus'); },
    productEdit: function (id) { productEdit = Number(id); show(); window.scrollTo(0, 0); $('#sp-name').trigger('focus'); },
    productCancel: function () { productEdit = null; show(); },
    productRemove: function (id) {
      var p = products.filter(function (x) { return x.id === Number(id); })[0]; if (!p) return;
      ST.modal('<h3>Remove this product?</h3><p>' + e(p.name) + ' will no longer be shown to travellers.</p><div class="hrow"><span class="sp"></span><button type="button" class="btn btn-plain" data-close>Keep it</button><button type="button" class="btn btn-danger" data-act="productRemoveYes" data-id="' + p.id + '">Remove</button></div>');
    },
    productRemoveYes: function (id) { ST.api('DELETE', 'supplier/me/products/' + id).done(function () { ST.closeModal(); ST.toast('Product removed'); openProducts(productsOf); }); },
    preview: function (id) {
      var l = mine(id); if (!l) return;
      ST.api('GET', 'supplier/me/listings/' + l.id + '/products', null, true).always(function (list) {
        ST.modal(ST.listingDetail(l) + ST.productList($.isArray(list) ? list.filter(function (p) { return p.isActive; }) : []) + '<div class="hrow"><span class="sp"></span><button type="button" class="btn btn-dark" data-close>Close</button></div>');
      });
    },
    removeYes: function (id) { ST.api('DELETE', 'supplier/me/listings/' + id).done(function (s) { saved(s, 'Listing removed'); }); },
    promo: function (id) {
      var l = mine(id); if (!l) return; var p = l.promotion || { title: '', text: '', until: '' };
      var today = new Date(Date.now() + 5.5 * 3600e3).toISOString().slice(0, 10);
      ST.modal('<form data-form="promo" data-id="' + l.id + '" class="vstack gap-3"><h3>Promotion for ' + e(l.name) + '</h3><p class="small mute">Travellers see a red Promotion badge on your tile and this offer under your name.</p>' +
        field('pr-title', 'Offer, in a few words', text('pr-title', 'title', p.title, 'required minlength="3" maxlength="80" placeholder="20% off rooms this month"')) +
        field('pr-text', 'Details (optional)', '<textarea class="form-control" id="pr-text" name="text" rows="3" maxlength="300" placeholder="Book direct and mention Safe Travel.">' + e(p.text) + '</textarea>') +
        field('pr-until', 'Last day (leave empty for no end date)', text('pr-until', 'until', p.until || '', 'type="date" min="' + today + '"')) +
        '<div class="hrow">' + (l.promotion ? '<button type="button" class="btn btn-plain" data-act="promoEnd" data-id="' + l.id + '">End promotion</button>' : '') + '<span class="sp"></span><button type="button" class="btn btn-plain" data-close>Cancel</button><button class="btn btn-danger" type="submit">Save promotion</button></div></form>');
    },
    promoEnd: function (id) { ST.api('DELETE', 'supplier/me/listings/' + id + '/promotion').done(function (s) { saved(s, 'Promotion ended'); }); }
  };
  $(document).on('click', '[data-act]', function () { var a = acts[this.dataset.act]; if (a) a(this.dataset.id); });
  $(document).on('click', '[data-act="pay"]', function () {
    var b = $(this).prop('disabled', true);
    ST.api('POST', 'supplier/me/pay').always(function () { b.prop('disabled', false); }).done(function (p) {
      if (p.status === 'Paid') ST.toast('Payment received. Your listing is live.'); else ST.toast('The payment did not go through. No money was taken. Try again.', true);
      route();
    });
  });
  $(document).on('submit', 'form[data-form]', function (ev) {
    ev.preventDefault();
    var form = this;
    if (form.dataset.form === 'listing') {
      var d = ST.form(form), id = Number(form.dataset.id); d.categoryId = Number(d.categoryId); d.latitude = d.latitude || null; d.longitude = d.longitude || null;
      ST.busy(form, id ? ST.api('PUT', 'supplier/me/listings/' + id, d) : ST.api('POST', 'supplier/me/listings', d))
        .done(function (s) { saved(s, id ? 'Saved. Travellers see the change now.' : 'Listing added.'); });
    } else if (form.dataset.form === 'product') {
      var pd = ST.form(form), pid = Number(form.dataset.id), file = $('#sp-file')[0].files[0];
      if (file && file.size > 2 * 1024 * 1024) { ST.toast('Choose a picture up to 2 MB.', true); return; }
      ST.busy(form, (pid ? ST.api('PUT', 'supplier/me/products/' + pid, pd) : ST.api('POST', 'supplier/me/listings/' + productsOf + '/products', pd)).then(function (p) {
        if (!file) return p;
        var fd = new FormData(); fd.append('file', file);
        return ST.api('POST', 'supplier/me/products/' + p.id + '/image', fd);
      })).done(function () { ST.toast(pid ? 'Product saved' : 'Product added'); openProducts(productsOf); });
    } else if (form.dataset.form === 'promo') {
      var p = ST.form(form);
      ST.busy(form, ST.api('PUT', 'supplier/me/listings/' + form.dataset.id + '/promotion', p)).done(function (s) { saved(s, 'Promotion saved. The red badge is on your tile now.'); });
    } else if (form.dataset.form === 'doc') {
      var file = form.elements.file.files[0];
      if (!file) { ST.toast('Choose a file first.', true); return; }
      if (file.size > 5 * 1024 * 1024) { ST.toast('Choose a file up to 5 MB.', true); return; }
      var fd = new FormData(); fd.append('name', form.elements.name.value); fd.append('file', file);
      ST.busy(form, ST.api('POST', 'supplier/me/documents', fd)).done(function () { ST.toast('Uploaded. The admin team will check it.'); route(); });
    }
  });

  ST.api('GET', 'categories', null, true).done(function (c) { cats = c; }).always(route);
})(jQuery);
