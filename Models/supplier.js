/* Admin Portal (Supplier): a business registers, keeps its listing up to date and pays the annual fee. */
(function () {
  var ui = { page: 'overview', register: false };
  var DISTRICTS = ['Colombo', 'Gampaha', 'Kandy', 'Galle', 'Matara', 'Badulla', 'Matale', 'Nuwara Eliya', 'Hambantota', 'Anuradhapura', 'Trincomalee', 'Jaffna'];
  var DC = { Colombo: [6.93, 79.86], Gampaha: [7.09, 79.99], Kandy: [7.29, 80.63], Galle: [6.05, 80.22], Matara: [5.95, 80.54], Badulla: [6.99, 81.05], Matale: [7.47, 80.62], 'Nuwara Eliya': [6.95, 80.79], Hambantota: [6.12, 81.12], Anuradhapura: [8.31, 80.40], Trincomalee: [8.57, 81.23], Jaffna: [9.66, 80.02] };

  function me() { var s = Store.get(); return Store.supplier(s.me) || s.suppliers[0]; }
  function head(t, sub) { return '<h2>' + t + '</h2><p class="sub">' + sub + '</p>'; }
  function daysLeft(iso) { if (!iso) return null; return Math.ceil((new Date(iso + 'T00:00:00') - new Date()) / 864e5); }
  function minus(iso, n) { var d = new Date(iso + 'T00:00:00'); d.setDate(d.getDate() - n); return d.toISOString().slice(0, 10); }
  function bizCats(sel) { return Store.get().categories.filter(function (c) { return c.kind === 'business'; }).map(function (c) { return '<option value="' + c.id + '"' + (sel === c.id ? ' selected' : '') + '>' + c.icon + ' ' + esc(c.name) + '</option>'; }).join(''); }

  function stage(x, l) {
    if (x.status === 'rejected') return -1;
    if (x.status === 'pending') return 1;
    if (!x.paidUntil || (l && (l.status === 'unpaid' || l.status === 'lapsed'))) return 3;
    return 5;
  }

  function overview(x, l) {
    var n = stage(x, l), d = daysLeft(x.paidUntil);
    var steps = ['Registered', 'Documents checked', 'Approved', 'Annual fee paid', 'Live on the map'];
    var msg = n === -1 ? ['bad', 'Your registration was not approved. Check your documents and contact the support team.'] :
      n === 1 ? ['warn', 'Your registration is with the admin team. Most reviews take two working days.'] :
        n === 3 ? ['info', l && l.status === 'lapsed' ? 'Your listing is hidden because the annual fee has lapsed. Pay to bring it back.' : 'You are approved. Pay the annual fee to go live.'] :
          ['ok', 'Your listing is live on the web portal and in the mobile app.'];
    return head(esc(x.business), 'Your listing status, results and renewal date.') +
      '<div class="panel"><div class="body row"><span class="pill ' + msg[0] + '">' + (n === 5 ? 'Live' : n === 3 ? 'Action needed' : n === 1 ? 'In review' : 'Not approved') + '</span><span>' + msg[1] + '</span><span class="sp"></span>' +
      (n === 3 ? '<button type="button" class="btn btn-red" data-act="sGo" data-page="payment">Pay ' + lkr(Store.get().settings.annualFee) + '</button>' : '') + '</div></div>' +
      '<div class="steps">' + steps.map(function (t, i) { return '<div class="step ' + (i < n ? 'done' : i === n ? 'now' : '') + '"><span class="small mute">Step ' + (i + 1) + '</span><b>' + (i < n ? '✓ ' : '') + t + '</b></div>'; }).join('') + '</div>' +
      '<div class="stats"><div class="stat"><b>' + x.views.toLocaleString() + '</b><span>Listing views</span></div><div class="stat"><b>' + x.calls + '</b><span>Calls from the app</span></div><div class="stat"><b>' + x.directions + '</b><span>Direction requests</span></div>' +
      '<div class="stat"><b>' + (l && l.rating ? '★ ' + l.rating.toFixed(1) : '-') + '</b><span>' + (l && l.reviews ? l.reviews + ' traveller reviews' : 'No reviews yet') + '</span></div>' +
      '<div class="stat"><b>' + (d == null ? '-' : d < 0 ? 'Lapsed' : d + ' days') + '</b><span>' + (x.paidUntil ? 'Until renewal on ' + x.paidUntil : 'Not paid yet') + '</span></div></div>';
  }

  function listing(x, l) {
    if (!l) return head('My listing', 'No listing yet.');
    return head('My listing', 'This is what travellers see on the web portal and in the app.') +
      '<form class="panel" data-form="sListing"><div class="body"><div class="grid2"><div class="field"><label for="sl-name">Business name</label><input id="sl-name" name="name" required value="' + esc(l.name) + '"></div>' +
      '<div class="field"><label for="sl-cat">Category</label><select id="sl-cat" name="cat">' + bizCats(l.cat) + '</select></div>' +
      '<div class="field"><label for="sl-town">Town</label><input id="sl-town" name="town" required value="' + esc(l.town) + '"></div><div class="field"><label for="sl-phone">Phone</label><input id="sl-phone" name="phone" required value="' + esc(l.phone) + '"></div></div>' +
      '<div class="field" style="margin-top:12px"><label for="sl-hours">Opening hours</label><input id="sl-hours" name="hours" value="' + esc(l.hours) + '"></div>' +
      '<div class="field" style="margin-top:12px"><label for="sl-desc">Description and safety arrangements</label><textarea id="sl-desc" name="desc" rows="3">' + esc(l.desc) + '</textarea></div>' +
      '<div class="row" style="margin-top:14px"><button class="btn btn-red" type="submit">Save listing</button><button type="button" class="btn" data-open="' + l.id + '">Preview</button><span class="small mute">A change of name or category goes back to the admin team for a quick check.</span></div></div></form>';
  }

  function documents(x) {
    return head('Documents', 'Upload the documents the admin team needs to verify your business.') +
      '<section class="panel"><div class="tw"><table class="t"><thead><tr><th>Document</th><th>Status</th></tr></thead><tbody>' + x.docs.map(function (d) { return '<tr><td><b>' + esc(d.name) + '</b></td><td><span class="pill ' + (d.status === 'verified' ? 'ok">Verified' : 'warn">Uploaded, waiting for a check') + '</span></td></tr>'; }).join('') + '</tbody></table></div>' +
      '<form data-form="sDoc" class="body row"><div class="field" style="flex:1 1 220px"><label for="sd-name">Document type</label><select id="sd-name" name="name"><option>Tourist board licence</option><option>Insurance certificate</option><option>Food safety certificate</option><option>Guide licence</option><option>Owner identity document</option></select></div><button class="btn btn-ink" type="submit" style="align-self:flex-end">Upload document</button><span class="small mute" style="align-self:flex-end">Model only. No file is sent.</span></form></section>';
  }

  function payment(x, l) {
    var s = Store.get(), fee = s.settings.annualFee, d = daysLeft(x.paidUntil);
    var mine = s.payments.filter(function (p) { return p.who === x.business; });
    var can = x.status === 'approved';
    return head('Payment and renewal', 'One annual fee keeps your listing on the map. Pay by card, mobile wallet or internet banking.') +
      '<div class="stats"><div class="stat"><b>' + lkr(fee) + '</b><span>Annual fee</span></div><div class="stat"><b>' + (x.paidUntil || '-') + '</b><span>Paid until</span></div><div class="stat"><b>' + (d == null ? '-' : d < 0 ? 'Lapsed' : d + ' days') + '</b><span>Until renewal</span></div></div>' +
      '<div class="panel"><div class="body row">' + (can ? '<button type="button" class="btn btn-red" data-act="sPay">' + (x.paidUntil ? 'Renew for one year' : 'Pay and go live') + ' · ' + lkr(fee) + '</button><span class="small mute">Model only. No payment is taken.</span>' : '<span class="pill warn">Payment opens after approval</span><span>The admin team is still checking your registration.</span>') + '</div></div>' +
      (x.paidUntil ? '<section class="panel"><header><h3>Your renewal reminders</h3></header><div class="tw"><table class="t"><tbody>' + [[30, 'Email'], [14, 'Email and SMS'], [7, 'Email, SMS and app notification'], [0, 'Email and SMS on the renewal day']].map(function (r) { return '<tr><td class="num">' + minus(x.paidUntil, r[0]) + '</td><td>' + (r[0] ? r[0] + ' days before' : 'Renewal day') + '</td><td>' + r[1] + '</td></tr>'; }).join('') + '</tbody></table></div></section>' : '') +
      '<section class="panel"><header><h3>Payment history</h3></header><div class="tw"><table class="t"><thead><tr><th>No.</th><th>Date</th><th>Amount</th><th>Status</th></tr></thead><tbody>' + (mine.map(function (p) { return '<tr><td class="num">' + p.id + '</td><td class="num">' + p.date + '</td><td class="num">' + lkr(p.amount) + '</td><td><span class="pill ' + (p.status === 'Paid' ? 'ok' : 'bad') + '">' + p.status + '</span></td></tr>'; }).join('') || '<tr><td colspan="4" class="mute">No payments yet.</td></tr>') + '</tbody></table></div></section>';
  }

  function register() {
    return head('Register your business', 'Tell us about your business. The admin team checks your documents, then you pay the annual fee and go live.') +
      '<form class="panel" data-form="sRegister"><div class="body"><div class="grid2"><div class="field"><label for="sr-biz">Business name</label><input id="sr-biz" name="business" required></div><div class="field"><label for="sr-cat">Category</label><select id="sr-cat" name="cat">' + bizCats('stay') + '</select></div>' +
      '<div class="field"><label for="sr-town">Town</label><input id="sr-town" name="town" required></div><div class="field"><label for="sr-district">District</label><select id="sr-district" name="district">' + DISTRICTS.map(function (d) { return '<option>' + d + '</option>'; }).join('') + '</select></div>' +
      '<div class="field"><label for="sr-contact">Your name</label><input id="sr-contact" name="contact" required></div><div class="field"><label for="sr-phone">Mobile number</label><input id="sr-phone" name="phone" required inputmode="tel"></div>' +
      '<div class="field"><label for="sr-email">Email</label><input id="sr-email" name="email" type="email" required></div><div class="field"><label for="sr-hours">Opening hours</label><input id="sr-hours" name="hours" placeholder="Daily 9:00 to 18:00"></div></div>' +
      '<div class="field" style="margin-top:12px"><label for="sr-desc">Short description</label><textarea id="sr-desc" name="desc" rows="2"></textarea></div>' +
      '<div class="row" style="margin-top:14px"><button class="btn btn-red" type="submit">Send registration</button><button type="button" class="btn" data-act="sCancel">Cancel</button></div></div></form>';
  }

  var pages = [['overview', '🏠', 'Overview'], ['listing', '📝', 'My listing'], ['documents', '📄', 'Documents'], ['payment', '💳', 'Payment and renewal']];

  Apps.supplier = {
    render: function () {
      var s = Store.get(), x = me(), l = Store.listingOf(x.id);
      var body = ui.register ? register() : ui.page === 'listing' ? listing(x, l) : ui.page === 'documents' ? documents(x) : ui.page === 'payment' ? payment(x, l) : overview(x, l);
      return '<div class="adm"><nav class="side" aria-label="Supplier portal"><div class="who"><b>Supplier Portal</b>Signed in as<select class="inp" id="s-who" data-on="sWho" style="margin-top:6px;color:var(--ink)" aria-label="View as supplier">' +
        s.suppliers.map(function (v) { return '<option value="' + v.id + '"' + (v.id === x.id ? ' selected' : '') + '>' + esc(v.business) + '</option>'; }).join('') + '</select></div>' +
        pages.map(function (p) { return '<button type="button" data-act="sGo" data-page="' + p[0] + '"' + (!ui.register && ui.page === p[0] ? ' aria-current="page"' : '') + '><span>' + p[1] + '</span>' + p[2] + '</button>'; }).join('') +
        '<button type="button" data-act="sRegister"' + (ui.register ? ' aria-current="page"' : '') + '><span>➕</span>Register a new business</button></nav><div class="main">' + body + '</div></div>';
    }
  };

  Acts.sGo = function (el) { ui.page = el.dataset.page; ui.register = false; render(); window.scrollTo(0, 0); };
  Acts.sRegister = function () { ui.register = true; render(); };
  Acts.sCancel = function () { ui.register = false; render(); };
  Acts.sWho = function (el) { Store.get().me = Number(el.value); ui.register = false; ui.page = 'overview'; Store.save(); };
  Acts.sPay = function () {
    var s = Store.get(), x = me(), l = Store.listingOf(x.id);
    var from = x.paidUntil && daysLeft(x.paidUntil) > 0 ? new Date(x.paidUntil + 'T00:00:00') : new Date();
    from.setFullYear(from.getFullYear() + 1);
    x.paidUntil = from.toISOString().slice(0, 10);
    if (l) l.status = 'published';
    s.payments.unshift({ id: s.payments[0].id + 1, date: new Date().toISOString().slice(0, 10), kind: 'Supplier fee', who: x.business, amount: s.settings.annualFee, status: 'Paid' });
    Store.log(x.business, 'Paid the annual fee, listing live until ' + x.paidUntil);
    Store.save(); toast('Paid. Your listing is live until ' + x.paidUntil + '.');
  };
  Forms.sListing = function (d) {
    var x = me(), l = Store.listingOf(x.id);
    l.name = d.name; l.cat = d.cat; l.town = d.town; l.phone = d.phone; l.hours = d.hours; l.desc = d.desc; x.business = d.name;
    Store.log(x.business, 'Updated their listing'); Store.save(); toast('Saved. Travellers see the update now.');
  };
  Forms.sDoc = function (d) { var x = me(); x.docs.push({ name: d.name, status: 'uploaded' }); Store.log(x.business, 'Uploaded "' + d.name + '"'); Store.save(); toast('Uploaded. The admin team will check it.'); };
  Forms.sRegister = function (d) {
    var s = Store.get(), id = Store.id(), c = DC[d.district] || DC.Colombo;
    s.suppliers.push({ id: id, business: d.business, contact: d.contact, email: d.email, phone: d.phone, status: 'pending', paidUntil: '', docs: [{ name: 'Business registration', status: 'uploaded' }], views: 0, calls: 0, directions: 0 });
    s.listings.push({ id: Store.id(), cat: d.cat, name: d.business, town: d.town, district: d.district, phone: d.phone, hours: d.hours || 'Not given', lat: c[0] + (Math.random() - 0.5) * 0.1, lng: c[1] + (Math.random() - 0.5) * 0.1, status: 'pending', supplierId: id, verified: false, rating: 0, reviews: 0, featured: false, desc: d.desc });
    s.me = id; ui.register = false; ui.page = 'overview';
    Store.log(d.business, 'Registered as a new supplier'); Store.save();
    toast('Registration sent. Approve it under Suppliers in the Admin Portal (Internal).');
  };
})();
