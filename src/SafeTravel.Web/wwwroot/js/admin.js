/* Admin Portal (Internal): staff control everything the Web Portal and the mobile apps show.
   Each screen loads its data from /api/v1/admin with AJAX and sends every change back the same way. */
(function ($) {
  'use strict';
  var e = ST.esc, lkr = ST.lkr, $main = $('#main');
  var roles = String($('#admin').data('roles') || '').split(',');
  function has() { return roles.indexOf('SuperAdmin') >= 0 || [].slice.call(arguments).some(function (r) { return roles.indexOf(r) >= 0; }); }
  var can = {
    suppliersRead: has('SupplierManager', 'CallCentreAgent', 'FinanceOfficer'), suppliers: has('SupplierManager'),
    content: has('ContentReviewer'), alerts: has('AlertsOfficer'), finance: has('FinanceOfficer'), admin: has()
  };
  var ui = { kind: 'help', edit: null }, cache = {};
  var DISTRICTS = ['Ampara', 'Anuradhapura', 'Badulla', 'Batticaloa', 'Colombo', 'Galle', 'Gampaha', 'Hambantota', 'Jaffna', 'Kalutara', 'Kandy', 'Kegalle', 'Kilinochchi', 'Kurunegala', 'Mannar', 'Matale', 'Matara', 'Monaragala', 'Mullaitivu', 'Nuwara Eliya', 'Polonnaruwa', 'Puttalam', 'Ratnapura', 'Trincomalee', 'Vavuniya'];
  var ROLE_NAMES = { SuperAdmin: 'Super admin', ContentReviewer: 'Content reviewer', AlertsOfficer: 'Alerts officer', SupplierManager: 'Supplier manager', FinanceOfficer: 'Finance officer', CallCentreAgent: 'Call centre agent' };

  // ----- small building blocks -----
  function st(status) {
    var m = { published: ['ok', 'Live'], pending: ['warn', 'Awaiting review'], unpaid: ['info', 'Awaiting payment'], lapsed: ['bad', 'Lapsed'], hidden: ['', 'Hidden'], rejected: ['bad', 'Rejected'], approved: ['ok', 'Approved'] };
    var x = m[status] || ['', status];
    return '<span class="pill ' + x[0] + '">' + e(x[1]) + '</span>';
  }
  function head(t, sub) { return '<h2>' + t + '</h2><p class="sub">' + sub + '</p>'; }
  function panel(title, body, extra) { return '<section class="panel"><header><h3>' + title + '</h3><span class="sp"></span>' + (extra || '') + '</header>' + body + '</section>'; }
  function table(cols, rows) {
    return '<div class="tw"><table class="t"><thead><tr>' + cols.map(function (c) { return '<th scope="col">' + c + '</th>'; }).join('') + '</tr></thead><tbody>' +
      (rows.join('') || '<tr><td colspan="' + cols.length + '" class="mute">Nothing here yet.</td></tr>') + '</tbody></table></div>';
  }
  function sw(act, id, on, label, allowed) {
    return '<div class="form-check form-switch mb-0"><input class="form-check-input" type="checkbox" role="switch" id="sw-' + act + '-' + id + '" data-sw="' + act + '" data-id="' + id + '"' + (on ? ' checked' : '') + (allowed === false ? ' disabled' : '') +
      (label ? '' : ' aria-label="On or off"') + '>' + (label ? '<label class="form-check-label fw-semibold" for="sw-' + act + '-' + id + '">' + e(label) + '</label>' : '') + '</div>';
  }
  function field(id, label, input, style) { return '<div class="field"' + (style ? ' style="' + style + '"' : '') + '><label class="form-label" for="' + id + '">' + label + '</label>' + input + '</div>'; }
  function text(id, name, value, attrs) { return '<input class="form-control" id="' + id + '" name="' + name + '" value="' + e(value) + '" ' + (attrs || '') + '>'; }
  function select(id, name, options) { return '<select class="form-select" id="' + id + '" name="' + name + '">' + options + '</select>'; }
  function btn(act, id, label, cls) { return '<button type="button" class="btn btn-sm ' + (cls || 'btn-plain') + '" data-act="' + act + '" data-id="' + id + '">' + label + '</button>'; }
  function acts(html) { return '<td><div class="acts">' + html + '</div></td>'; }
  function done(msg) { ST.toast(msg); return show(); }

  // ----- screens -----
  var screens = {};

  screens.dash = function () {
    return ST.api('GET', 'admin/dashboard').then(function (d) {
      var max = Math.max.apply(null, d.weeklyRegistrations.concat([1]));
      var rows = d.needsReview.map(function (x) {
        return '<tr><td><b>' + e(x.businessName) + '</b></td><td>New supplier, ' + x.documents.length + ' document' + (x.documents.length === 1 ? '' : 's') + ' to check</td>' +
          acts(can.suppliers ? btn('approve', x.id, 'Approve', 'btn-dark') + btn('reject', x.id, 'Reject') : '') + '</tr>';
      }).concat(d.lapsed.map(function (l) {
        return '<tr><td><b>' + e(l.name) + '</b></td><td>Annual fee not renewed, hidden from the public</td>' + acts(can.suppliersRead ? btn('remind', l.supplierId, 'Send reminder') : '') + '</tr>';
      }));
      return head('Dashboard', 'What needs attention today, across suppliers, listings, alerts and payments.') +
        '<div class="stats"><div class="stat"><b>' + d.liveListings + '</b><span>Listings live on the portal and app</span></div><div class="stat"><b>' + d.pendingSuppliers + '</b><span>Suppliers awaiting review</span></div>' +
        '<div class="stat"><b>' + d.activeAlerts + '</b><span>Active alerts</span></div><div class="stat"><b>' + lkr(d.collectedThisMonth) + '</b><span>Collected this month</span></div></div>' +
        panel('Needs attention', table(['Item', 'Why', ''], rows)) +
        '<div class="grid2">' + panel('New supplier registrations, last ' + d.weeklyRegistrations.length + ' weeks', '<div class="body"><div class="bars" role="img" aria-label="Registrations per week: ' + d.weeklyRegistrations.join(', ') + '">' +
          d.weeklyRegistrations.map(function (v) { return '<i style="height:' + (v / max * 100) + '%" title="' + v + '"></i>'; }).join('') + '</div><p class="small mute mt-2">Oldest week on the left, this week in red.</p></div>') +
        panel('Latest activity', table(['When', 'Who', 'What'], d.latest.map(function (a) { return '<tr><td class="small num text-nowrap">' + ST.dateTime(a.whenUtc) + '</td><td class="small">' + e(a.userName) + '</td><td class="small">' + e(a.action) + '</td></tr>'; }))) + '</div>';
    });
  };

  screens.suppliers = function () {
    return ST.api('GET', 'admin/suppliers', { pageSize: 200 }).then(function (r) {
      cache.suppliers = r.items;
      return head('Suppliers', 'Businesses that registered through the supplier portal. Approve them after checking their documents. A listing goes live once the annual fee is paid.') +
        panel('All suppliers', table(['Business', 'Contact', 'Documents', 'Status', 'Paid until', ''], r.items.map(function (x) {
          var l = x.listing || {};
          var a = x.status === 'pending' && can.suppliers ? btn('approve', x.id, 'Approve', 'btn-dark') + btn('reject', x.id, 'Reject') :
            x.status === 'approved' ? btn('remind', x.id, 'Send reminder') + (l.id ? btn('view', x.id, 'View') : '') : '';
          return '<tr><td><b>' + e(x.businessName) + '</b><span class="small mute">' + e(l.category || '') + ' · ' + e(l.town || '') + '</span></td>' +
            '<td>' + e(x.contactName) + '<span class="small mute">' + e(x.phone) + ' · ' + e(x.email) + '</span></td>' +
            '<td>' + (x.documents.map(function (d) { return '<a class="pill ' + (d.status === 'verified' ? 'ok' : 'warn') + '" href="/api/v1/documents/' + d.id + '" title="' + e(d.fileName) + ', ' + e(d.status) + '">' + e(d.name) + '</a>'; }).join(' ') || '<span class="mute small">None yet</span>') + '</td>' +
            '<td>' + st(x.status === 'approved' ? (l.status || 'approved') : x.status) + (x.rejectReason ? '<span class="small mute">' + e(x.rejectReason) + '</span>' : '') + '</td>' +
            '<td class="num text-nowrap">' + (x.paidUntil ? ST.date(x.paidUntil) : '<span class="mute">Not paid</span>') + '</td>' + acts(a) + '</tr>';
        })), '<span class="small mute">' + r.total + ' in total</span>');
    });
  };

  function catOptions(kind, sel) {
    return (cache.categories || []).filter(function (c) { return c.kind === kind; }).map(function (c) { return '<option value="' + c.id + '"' + (sel === c.id ? ' selected' : '') + '>' + e(c.icon) + ' ' + e(c.name) + '</option>'; }).join('');
  }
  screens.listings = function () {
    return $.when(ST.api('GET', 'admin/listings', { kind: ui.kind, pageSize: 200 }), ST.api('GET', 'admin/categories')).then(function (a, b) {
      var list = a[0].items, biz = ui.kind === 'business'; cache.listings = list; cache.categories = b[0];
      var x = ui.edit ? list.filter(function (l) { return l.id === ui.edit; })[0] : null;
      var form = can.suppliers && (!biz || x) ? '<form class="panel" data-form="listing"><header><h3>' + (x ? 'Edit ' + e(x.name) : 'Add a help point') + '</h3></header><div class="body"><div class="grid2">' +
        field('al-name', 'Name', text('al-name', 'name', x ? x.name : '', 'required maxlength="160"')) +
        field('al-cat', 'Category', select('al-cat', 'categoryId', catOptions(ui.kind, x ? x.categoryId : 0))) +
        field('al-town', 'Town', text('al-town', 'town', x ? x.town : '', 'required maxlength="80"')) +
        field('al-district', 'District', select('al-district', 'district', DISTRICTS.map(function (d) { return '<option' + (x && x.district === d ? ' selected' : '') + '>' + d + '</option>'; }).join(''))) +
        field('al-phone', 'Phone', text('al-phone', 'phone', x ? x.phone : '', 'maxlength="30"')) +
        field('al-hours', 'Opening hours', text('al-hours', 'hours', x ? x.hours : '', 'maxlength="160"')) +
        field('al-lat', 'Latitude (optional)', text('al-lat', 'latitude', x ? x.latitude : '', 'type="number" step="any" min="5.5" max="10.2"')) +
        field('al-lng', 'Longitude (optional)', text('al-lng', 'longitude', x ? x.longitude : '', 'type="number" step="any" min="79" max="82.5"')) + '</div>' +
        '<p class="small mute mt-2">Leave the map position empty to place the pin at the centre of the district.</p>' +
        '<div class="hrow mt-3"><button class="btn btn-danger" type="submit">' + (x ? 'Save changes' : 'Publish help point') + '</button>' + (x ? btn('editCancel', 0, 'Cancel') : '') + '</div></div></form>' : '';
      return head('Listings', 'Help points such as banks, SOS points and police stations are added by staff. Business listings come from suppliers. Both appear on the portal map and in the app.') +
        '<div class="hrow mt-3"><button type="button" class="chip' + (biz ? '' : ' on') + '" data-act="kind" data-id="help">Help points</button><button type="button" class="chip' + (biz ? ' on' : '') + '" data-act="kind" data-id="business">Business listings</button></div>' + form +
        panel(biz ? 'Business listings' : 'Help points', table(['Name', 'Category', 'Town', 'Status', biz ? 'Featured' : 'Phone', ''], list.map(function (l) {
          var toggle = l.status === 'published' ? btn('hide', l.id, 'Hide') : l.status === 'hidden' ? btn('unhide', l.id, 'Show') : '';
          return '<tr><td><b>' + e(l.name) + '</b>' + (l.promotion ? '<span class="small"><span class="pill bad">Promotion</span> ' + e(l.promotion.title) + '</span>' : '') + '</td><td>' + e(l.icon) + ' ' + e(l.category) + '</td><td>' + e(l.town) + '</td><td>' + st(l.status) + '</td>' +
            '<td class="num">' + (biz ? sw('featured', l.id, l.isFeatured, '', can.suppliers) : e(l.phone)) + '</td>' +
            acts(can.suppliers ? (l.promotion ? btn('promoDel', l.id, 'Remove promotion') : '') + btn('edit', l.id, 'Edit') + toggle + (biz ? '' : btn('del', l.id, 'Delete')) : '') + '</tr>';
        })), '<span class="small mute">' + a[0].total + ' in total</span>');
    });
  };

  screens.categories = function () {
    return ST.api('GET', 'admin/categories').then(function (cats) {
      cache.categories = cats;
      return head('Categories', 'Turn a category off and it disappears from the portal filters, the map and the app. Add new ones whenever you need them.') +
        (can.suppliers ? '<form class="panel" data-form="category"><header><h3>Add a category</h3></header><div class="body"><div class="hrow align-items-end">' +
          field('ac-name', 'Name', text('ac-name', 'name', '', 'required maxlength="80" placeholder="Tourist information centres"'), 'flex:2 1 200px') +
          field('ac-icon', 'Icon', text('ac-icon', 'icon', 'ℹ️', 'maxlength="8"'), 'flex:0 1 90px') +
          field('ac-kind', 'Type', select('ac-kind', 'kind', '<option value="help">Help point</option><option value="business">Business</option>'), 'flex:1 1 150px') +
          '<button class="btn btn-danger" type="submit">Add category</button></div></div></form>' : '') +
        panel('All categories', table(['Category', 'Type', 'Listings', 'Shown to the public'], cats.map(function (c) {
          return '<tr><td><b>' + e(c.icon) + ' ' + e(c.name) + '</b></td><td>' + (c.kind === 'help' ? 'Help point' : 'Business') + '</td><td class="num">' + c.listingCount + '</td><td>' + sw('category', c.id, c.isActive, '', can.suppliers) + '</td></tr>';
        })));
    });
  };

  screens.content = function () {
    var labels = { alerts: 'Live alerts panel', ads: 'Sponsor advertisements', map: 'Help points map', businesses: 'Verified businesses', topics: 'Safety topics', donate: 'Donate card', sponsors: 'Sponsors strip' };
    return ST.api('GET', 'content/home').then(function (c) {
      return head('Web Portal content', 'The public site has no fixed pages. The headline, sections, emergency numbers and topics below are what it shows.') +
        '<form class="panel" data-form="hero"><header><h3>Home page headline</h3></header><div class="body vstack gap-3">' +
        field('ah-title', 'Headline', text('ah-title', 'heroTitle', c.heroTitle, 'required maxlength="120"')) +
        field('ah-text', 'Text under the headline', '<textarea class="form-control" id="ah-text" name="heroText" rows="2" maxlength="400">' + e(c.heroText) + '</textarea>') +
        '<div><button class="btn btn-danger" type="submit">Publish to the portal</button></div></div></form>' +
        panel('Home page sections', '<div class="body hrow" style="gap:12px 24px">' + Object.keys(labels).map(function (k) { return sw('section', k, c.sections[k] !== false, labels[k]); }).join('') + '</div>') +
        '<form class="panel" data-form="numbers"><header><h3>Emergency numbers</h3><span class="sp"></span><span class="small mute">Shown in the top bar, the footer and the app</span></header><div class="body"><div class="grid2" id="numbers">' +
        c.emergencyNumbers.map(numberRow).join('') + '</div><div class="hrow mt-3"><button class="btn btn-danger" type="submit">Save numbers</button>' + btn('numberAdd', 0, 'Add a number') + '</div></div></form>' +
        '<form class="panel" data-form="topic"><header><h3>Safety topics</h3></header>' + table(['Topic', 'Summary', ''], c.topics.map(function (t) {
          return '<tr><td><b>' + e(t.icon) + ' ' + e(t.name) + '</b></td><td class="small">' + e(t.summary) + '</td>' + acts(btn('topicEdit', t.id, 'Do and avoid') + btn('topicDel', t.id, 'Remove')) + '</tr>';
        })) + '<div class="body hrow align-items-end">' + field('at-icon', 'Icon', text('at-icon', 'icon', '📘', 'maxlength="8"'), 'flex:0 1 80px') + field('at-name', 'New topic', text('at-name', 'name', '', 'required maxlength="120"'), 'flex:1 1 180px') +
        field('at-text', 'Summary', text('at-text', 'summary', '', 'required maxlength="400"'), 'flex:2 1 240px') + '<button class="btn btn-danger" type="submit">Add topic</button></div></form>';
    });
  };
  function numberRow(n) {
    n = n || { label: '', number: '' };
    return '<div class="hrow flex-nowrap num-row"><input class="form-control" data-k="label" value="' + e(n.label) + '" maxlength="60" required aria-label="Label"><input class="form-control num" data-k="number" value="' + e(n.number) + '" style="max-width:120px" maxlength="30" required aria-label="Number">' +
      '<button type="button" class="btn btn-sm btn-plain" data-act="numberDel" aria-label="Remove this number">✕</button></div>';
  }

  screens.alerts = function () {
    return ST.api('GET', 'admin/alerts').then(function (list) {
      return head('Alerts', 'Publish a warning by area. It appears on the portal home page and in the mobile apps at once.') +
        (can.alerts ? '<form class="panel" data-form="alert"><header><h3>Publish an alert</h3></header><div class="body"><div class="grid2">' +
          field('aa-type', 'Type', select('aa-type', 'type', ['Flood warning', 'Landslide watch', 'Red flag', 'Road closure', 'Storm warning', 'Health notice'].map(function (t) { return '<option>' + t + '</option>'; }).join(''))) +
          field('aa-level', 'Level', select('aa-level', 'level', '<option value="red">Red: act now</option><option value="amber" selected>Amber: be ready</option><option value="blue">Blue: be aware</option>')) +
          field('aa-area', 'Area', text('aa-area', 'area', '', 'required maxlength="120" placeholder="Nuwara Eliya"')) +
          field('aa-source', 'Source', text('aa-source', 'source', '', 'maxlength="120" placeholder="Dept. of Meteorology"')) + '</div>' +
          '<div class="mt-3">' + field('aa-title', 'Message', text('aa-title', 'title', '', 'required minlength="5" maxlength="240" placeholder="Strong winds expected tonight"')) + '</div>' +
          '<div class="mt-3"><button class="btn btn-danger" type="submit">Publish alert</button></div></div></form>' : '') +
        panel('All alerts', table(['Alert', 'Area', 'Source', 'Sent', 'Active'], list.map(function (a) {
          return '<tr><td><span class="dot ' + (a.level === 'red' ? '' : e(a.level)) + '"></span> <b>' + e(a.title) + '</b><span class="small mute">' + e(a.type) + '</span></td><td>' + e(a.area) + '</td><td>' + e(a.source) + '</td><td class="small text-nowrap">' + ST.ago(a.publishedUtc) + '</td><td>' + sw('alert', a.id, a.isActive, '', can.alerts) + '</td></tr>';
        })));
    });
  };

  screens.payments = function () {
    return $.when(ST.api('GET', 'admin/payments', { pageSize: 200 }), ST.api('GET', 'content/home')).then(function (a, b) {
      var s = a[0].summary, list = a[0].payments;
      return head('Payments', 'Donations and supplier annual fees, paid through the payment gateway.') +
        '<div class="stats"><div class="stat"><b>' + lkr(s.donations) + '</b><span>Donations</span></div><div class="stat"><b>' + lkr(s.supplierFees) + '</b><span>Supplier annual fees</span></div><div class="stat"><b>' + s.failed + '</b><span>Failed payments to follow up</span></div></div>' +
        '<form class="panel" data-form="fee"><header><h3>Supplier annual fee</h3></header><div class="body hrow align-items-end">' +
        field('af-fee', 'Fee per supplier, per year (LKR)', text('af-fee', 'fee', b[0].annualFee, 'type="number" min="0" max="1000000" step="100" required'), 'flex:0 1 220px') +
        '<button class="btn btn-danger" type="submit">Save fee</button><span class="small mute">Reminders go out 30, 14 and 7 days before renewal, and on the day.</span></div></form>' +
        panel('Transactions', table(['No.', 'Date', 'Type', 'From', 'Amount', 'Status', 'Reference'], list.items.map(function (p) {
          return '<tr><td class="num">' + p.id + '</td><td class="num text-nowrap">' + ST.dateTime(p.createdUtc) + '</td><td>' + e(p.kind) + '</td><td>' + e(p.payer) + '</td><td class="num text-nowrap">' + lkr(p.amount) + '</td><td><span class="pill ' + (p.status === 'Paid' ? 'ok' : p.status === 'Failed' ? 'bad' : 'warn') + '">' + e(p.status) + '</span></td><td class="mono">' + e(p.reference || '') + '</td></tr>';
        })), '<span class="small mute">' + list.total + ' in total</span>');
    });
  };

  function adSlot(slot, ad, sponsors) {
    ad = ad || { sponsorId: 0, headline: '', text: '', linkLabel: '', linkUrl: '', isActive: true, clicks: 0 };
    var id = 'ad' + slot, ro = can.content ? '' : ' disabled';
    return '<form class="adslot" data-form="ad" data-slot="' + slot + '"><div class="hrow"><b>Place ' + slot + '</b>' + (ad.id ? '<span class="pill ' + (ad.isActive ? 'ok">Showing' : '">Off') + '</span>' : '<span class="pill warn">Empty</span>') +
      '<span class="sp"></span><span class="small mute num">' + ad.clicks + ' click' + (ad.clicks === 1 ? '' : 's') + '</span></div>' +
      (ad.imageUrl ? '<img src="' + e(ad.imageUrl) + '" alt="Picture of advertisement ' + slot + '">' : '') +
      field(id + '-sp', 'Sponsor', '<select class="form-select" id="' + id + '-sp" name="sponsorId"' + ro + '>' + sponsors.map(function (s) { return '<option value="' + s.id + '"' + (s.id === ad.sponsorId ? ' selected' : '') + '>' + e(s.name) + (s.isActive ? '' : ' (switched off)') + '</option>'; }).join('') + '</select>') +
      field(id + '-h', 'Headline', text(id + '-h', 'headline', ad.headline, 'required minlength="3" maxlength="120"' + ro)) +
      field(id + '-t', 'Text', '<textarea class="form-control" id="' + id + '-t" name="text" rows="2" maxlength="240"' + ro + '>' + e(ad.text) + '</textarea>') +
      field(id + '-l', 'Button text', text(id + '-l', 'linkLabel', ad.id ? ad.linkLabel : '', 'maxlength="40" placeholder="Learn more"' + ro)) +
      field(id + '-u', 'Link (https://)', text(id + '-u', 'linkUrl', ad.linkUrl, 'type="url" required maxlength="300" pattern="https://.+" placeholder="https://"' + ro)) +
      '<div class="form-check form-switch mb-0"><input class="form-check-input" type="checkbox" role="switch" id="' + id + '-on" name="isActive"' + (ad.isActive ? ' checked' : '') + ro + '><label class="form-check-label fw-semibold" for="' + id + '-on">Show on the portal and in the app</label></div>' +
      (can.content ? '<div class="hrow"><button class="btn btn-danger" type="submit">Save</button></div>' +
        (ad.id ? '<div class="field"><label class="form-label" for="' + id + '-f">Picture (JPG or PNG, up to 2 MB, wide shape works best)</label><input class="form-control" type="file" id="' + id + '-f" accept=".jpg,.jpeg,.png" data-adfile="' + slot + '"></div>' +
          (ad.imageUrl ? '<div>' + btn('adImageDel', slot, 'Remove picture') + '</div>' : '') : '<p class="small mute">Save the text first, then add a picture.</p>') : '') + '</form>';
  }

  // ----- first aid guides: steps one per line, and an optional Vimeo or Streamable video -----
  screens.firstaid = function () {
    return ST.api('GET', 'admin/first-aid').then(function (guides) {
      cache.guides = guides;
      var g = ui.edit ? guides.filter(function (x) { return x.id === ui.edit; })[0] : null;
      var v = g || { title: '', icon: '🩹', steps: [], videoUrl: '', isActive: true };
      return head('First aid guides', 'Step-by-step guides on the First aid page of the portal and in the app. Add a Vimeo or Streamable link to show a video with a guide.') +
        (can.content ? '<form class="panel" data-form="guide"><header><h3>' + (g ? 'Edit ' + e(g.title) : 'Add a guide') + '</h3></header><div class="body vstack gap-3"><div class="hrow align-items-end">' +
          field('fg-icon', 'Icon', text('fg-icon', 'icon', v.icon, 'maxlength="8"'), 'flex:0 1 90px') +
          field('fg-title', 'Title', text('fg-title', 'title', v.title, 'required minlength="3" maxlength="120" placeholder="Severe bleeding"'), 'flex:2 1 240px') +
          field('fg-video', 'Video link, Vimeo or Streamable (optional)', text('fg-video', 'videoUrl', v.videoUrl || '', 'maxlength="300" placeholder="https://vimeo.com/123456789"'), 'flex:2 1 260px') + '</div>' +
          field('fg-steps', 'Steps, one per line', '<textarea class="form-control" id="fg-steps" name="steps" rows="6" maxlength="4000" required>' + e(v.steps.join('\n')) + '</textarea>') +
          '<div class="form-check form-switch mb-0"><input class="form-check-input" type="checkbox" role="switch" id="fg-on" name="isActive"' + (v.isActive ? ' checked' : '') + '><label class="form-check-label fw-semibold" for="fg-on">Show to the public</label></div>' +
          '<div class="hrow"><button class="btn btn-danger" type="submit">' + (g ? 'Save guide' : 'Add guide') + '</button>' + (g ? btn('editCancel', 0, 'Cancel') : '') + '</div></div></form>' : '') +
        panel('All guides', table(['Guide', 'Steps', 'Video', 'Shown to the public', ''], guides.map(function (x) {
          return '<tr><td><b>' + e(x.icon) + ' ' + e(x.title) + '</b></td><td class="num">' + x.steps.length + '</td><td>' + (x.videoUrl ? '<span class="pill ok">Video</span>' : '<span class="mute small">None</span>') + '</td><td>' + sw('guide', x.id, x.isActive, '', can.content) + '</td>' +
            acts(can.content ? btn('guideEdit', x.id, 'Edit') + btn('guideDel', x.id, 'Delete') : '') + '</tr>';
        })));
    });
  };

  screens.sponsors = function () {
    return $.when(ST.api('GET', 'admin/sponsors'), ST.api('GET', 'admin/ads')).then(function (a, b) {
      var list = a[0], ads = b[0];
      return head('Sponsors', 'Sponsors shown on the portal, and the three advertisement places. Switch a name on only after its agreement is signed.') +
        panel('Advertisements', list.length ? '<div class="adslots">' + [1, 2, 3].map(function (n) { return adSlot(n, ads.filter(function (x) { return x.slot === n; })[0], list); }).join('') + '</div>' : '<div class="body mute">Add a sponsor first.</div>',
          '<span class="small mute">Three places on the Web Portal home page and in the mobile app</span>') +
        (can.content ? '<form class="panel" data-form="sponsor"><header><h3>Add a sponsor</h3></header><div class="body hrow align-items-end">' + field('as-name', 'Name', text('as-name', 'name', '', 'required maxlength="120"'), 'flex:1 1 180px') +
          field('as-role', 'Role', text('as-role', 'role', '', 'required maxlength="120" placeholder="Insurance partner"'), 'flex:1 1 180px') + '<button class="btn btn-danger" type="submit">Add sponsor</button></div></form>' : '') +
        panel('Sponsor names', table(['Sponsor', 'Role', 'Shown on the portal'], list.map(function (x) { return '<tr><td><b>' + e(x.name) + '</b></td><td>' + e(x.role) + '</td><td>' + sw('sponsor', x.id, x.isActive, '', can.content) + '</td></tr>'; })));
    });
  };

  screens.users = function () {
    return ST.api('GET', 'admin/users').then(function (list) {
      return head('Users and roles', 'Each staff member sees only the pages their role allows. Switch an account off and it is signed out everywhere.') +
        '<form class="panel" data-form="user" autocomplete="off"><header><h3>Add a staff member</h3></header><div class="body"><div class="grid2">' +
        field('au-name', 'Full name', text('au-name', 'fullName', '', 'required maxlength="120"')) + field('au-email', 'Email', text('au-email', 'email', '', 'type="email" required maxlength="200"')) +
        field('au-role', 'Role', select('au-role', 'role', Object.keys(ROLE_NAMES).map(function (r) { return '<option value="' + r + '">' + ROLE_NAMES[r] + '</option>'; }).join(''))) +
        field('au-pass', 'First password (10 characters or more, with a capital letter and a number)', text('au-pass', 'password', '', 'type="password" required minlength="10" maxlength="100" autocomplete="new-password"')) +
        '</div><div class="mt-3"><button class="btn btn-danger" type="submit">Add staff member</button></div></div></form>' +
        panel('Staff', table(['Name', 'Role', 'Two-step sign-in', 'Active'], list.map(function (u) {
          return '<tr><td><b>' + e(u.name) + '</b><span class="small mute">' + e(u.email) + '</span></td><td>' + e(ROLE_NAMES[u.role] || u.role) + '</td><td><span class="pill ' + (u.twoFactor ? 'ok">On' : 'bad">Off') + '</span></td><td>' + sw('user', u.id, u.active) + '</td></tr>';
        }))) +
        panel('What each role can do', table(['Role', 'Can do'], [['Super admin', 'Everything, including staff accounts and settings'], ['Content reviewer', 'Edit and publish portal content, numbers, topics and sponsors'], ['Alerts officer', 'Publish and expire alerts'], ['Supplier manager', 'Approve suppliers and manage listings and categories'], ['Finance officer', 'See payments and suppliers, set the annual fee'], ['Call centre agent', 'Look up suppliers and listings, send renewal reminders']].map(function (r) { return '<tr><td><b>' + r[0] + '</b></td><td>' + r[1] + '</td></tr>'; })));
    });
  };

  screens.system = function () {
    var api = [['GET', '/api/v1/content/home', 'Headline, sections, topics, emergency numbers', 'Public'], ['GET', '/api/v1/categories', 'Categories that are switched on', 'Public'],
      ['GET', '/api/v1/listings', 'Listings by category, district, search and distance', 'Public'], ['GET', '/api/v1/alerts', 'Active alerts', 'Public'], ['POST', '/api/v1/donations', 'Make a donation', 'Public, rate limited'],
      ['POST', '/api/v1/auth/login', 'Sign in, returns a 15 minute token and a refresh token', 'Public, rate limited'], ['POST', '/api/v1/auth/refresh', 'Swap a refresh token for a new pair', 'Public, rate limited'],
      ['POST', '/api/v1/suppliers/register', 'Register a business', 'Public, rate limited'], ['GET', '/api/v1/supplier/me', 'Own business, listing and documents', 'Supplier'], ['PUT', '/api/v1/supplier/me/listing', 'Update own listing', 'Supplier'],
      ['POST', '/api/v1/supplier/me/pay', 'Pay the annual fee', 'Supplier'], ['POST', '/api/v1/admin/suppliers/{id}/approve', 'Approve a supplier', 'Supplier manager'], ['POST', '/api/v1/admin/alerts', 'Publish an alert', 'Alerts officer']];
    var sec = ['Sign-in locks for 15 minutes after 5 wrong attempts', 'Two-step sign-in with an authenticator app, available to every account', 'Mobile apps use 15 minute tokens with refresh tokens that work once', 'Browser changes need an anti-forgery token', 'Rate limits on every API call, tighter on sign-in and public forms', 'Every admin change is written to the audit log', 'Public content is cached in memory so alert peaks do not reach the database', 'Strict content security policy: no inline scripts, no third-party scripts'];
    return $.Deferred().resolve(head('Security and API', 'One REST API serves the Web Portal, both admin portals and the Android and iOS apps.') +
      panel('Two-step sign-in for your account', '<div class="body" id="twofa"><div class="hrow"><span>Add a second step with an authenticator app such as Google Authenticator or Microsoft Authenticator.</span><span class="sp"></span>' + btn('twoSetup', 0, 'Set up', 'btn-dark') + '</div></div>') +
      panel('Security rules', '<div class="body vstack gap-2">' + sec.map(function (x) { return '<div class="hrow flex-nowrap align-items-start"><span class="pill ok">On</span><span>' + x + '</span></div>'; }).join('') + '</div>') +
      panel('REST API', table(['Method', 'Endpoint', 'What it does', 'Who can call it'], api.map(function (r) { return '<tr><td><span class="pill ' + (r[0] === 'GET' ? 'info' : 'warn') + '">' + r[0] + '</span></td><td class="mono text-nowrap">' + r[1] + '</td><td>' + r[2] + '</td><td>' + r[3] + '</td></tr>'; })))).promise();
  };

  screens.audit = function () {
    return ST.api('GET', 'admin/audit', { pageSize: 200 }).then(function (r) {
      return head('Audit log', 'Who changed what, and when. Entries cannot be edited or deleted.') +
        panel('Latest activity', table(['When', 'Who', 'What'], r.items.map(function (a) { return '<tr><td class="num text-nowrap">' + ST.dateTime(a.whenUtc) + '</td><td>' + e(a.userName) + '</td><td>' + e(a.action) + '</td></tr>'; })), '<span class="small mute">Showing ' + r.items.length + ' of ' + r.total + '</span>');
    });
  };

  // ----- menu and routing: the page is kept in the address after #, so reload and back work -----
  var pages = [['dash', '📊', 'Dashboard', true], ['suppliers', '🏪', 'Suppliers', can.suppliersRead], ['listings', '📍', 'Listings', can.suppliersRead], ['categories', '🗂️', 'Categories', true],
    ['content', '🖥️', 'Web Portal content', can.content], ['firstaid', '🩹', 'First aid guides', true], ['alerts', '🔔', 'Alerts', true], ['payments', '💳', 'Payments', can.finance], ['sponsors', '🤝', 'Sponsors', true],
    ['users', '👥', 'Users and roles', can.admin], ['system', '🔐', 'Security and API', true], ['audit', '🧾', 'Audit log', can.admin]].filter(function (p) { return p[3]; });
  var current = 'dash', pending = 0;

  function menu() {
    $('#side-links').html(pages.map(function (p) {
      return '<a class="link" href="#' + p[0] + '"' + (p[0] === current ? ' aria-current="page"' : '') + '><span>' + p[1] + '</span>' + p[2] + (p[0] === 'suppliers' && pending ? '<span class="cnt">' + pending + '</span>' : '') + '</a>';
    }).join(''));
  }
  function show() {
    var page = current;
    // A slow answer for a page the user has already left must not replace the page they are on now.
    return screens[page]().done(function (html) { if (page !== current) return; $main.html(html); menu(); });
  }
  function route() {
    var h = location.hash.replace('#', '');
    current = pages.some(function (p) { return p[0] === h; }) ? h : 'dash';
    ui.edit = null; menu();
    show().done(function () { window.scrollTo(0, 0); });
    ST.api('GET', 'admin/dashboard', null, true).done(function (d) { pending = d.pendingSuppliers; menu(); });
  }
  $(window).on('hashchange', route);

  // ----- actions -----
  var act = {
    approve: function (id) { ST.api('POST', 'admin/suppliers/' + id + '/approve').done(function () { pending = Math.max(0, pending - 1); done('Approved. The supplier can now pay the annual fee in the supplier portal.'); }); },
    reject: function (id) {
      ST.modal('<form data-form="reject" data-id="' + id + '" class="vstack gap-3"><h3>Reject this supplier</h3>' + field('rj-reason', 'Reason, sent to the supplier', '<textarea class="form-control" id="rj-reason" name="reason" rows="3" maxlength="400" required></textarea>') +
        '<div class="hrow"><span class="sp"></span><button type="button" class="btn btn-plain" data-close>Cancel</button><button class="btn btn-danger" type="submit">Reject</button></div></form>');
    },
    remind: function (id) { ST.api('POST', 'admin/suppliers/' + id + '/remind').done(function () { ST.toast('Renewal reminder sent to the supplier'); }); },
    view: function (id) {
      var x = (cache.suppliers || []).filter(function (s) { return s.id === id; })[0];
      if (x && x.listing) ST.modal(ST.listingDetail(x.listing) + '<dl class="kv"><dt>Views</dt><dd class="num">' + x.views + '</dd><dt>Calls</dt><dd class="num">' + x.calls + '</dd><dt>Directions</dt><dd class="num">' + x.directions + '</dd></dl><div class="hrow"><span class="sp"></span><button type="button" class="btn btn-dark" data-close>Close</button></div>');
    },
    kind: function (id) { ui.kind = id; ui.edit = null; show(); },
    edit: function (id) { ui.edit = id; show().done(function () { window.scrollTo(0, 0); $('#al-name').trigger('focus'); }); },
    editCancel: function () { ui.edit = null; show(); },
    hide: function (id) { ST.api('PUT', 'admin/listings/' + id + '/visible', { on: false }).done(function () { done('Hidden from the portal and app'); }); },
    unhide: function (id) { ST.api('PUT', 'admin/listings/' + id + '/visible', { on: true }).done(function () { done('Live on the portal and app'); }); },
    del: function (id) {
      var l = (cache.listings || []).filter(function (x) { return x.id === id; })[0];
      ST.modal('<h3>Delete this help point?</h3><p>' + e(l ? l.name : '') + ' will be removed from the portal and the app. This cannot be undone.</p><div class="hrow"><span class="sp"></span><button type="button" class="btn btn-plain" data-close>Keep it</button>' + btn('delYes', id, 'Delete', 'btn-danger') + '</div>');
    },
    delYes: function (id) { ST.api('DELETE', 'admin/listings/' + id).done(function () { ST.closeModal(); done('Help point deleted'); }); },
    topicEdit: function (id) {
      ST.api('GET', 'admin/content/topics').done(function (topics) {
        var t = topics.filter(function (x) { return x.id === id; })[0];
        if (!t) return;
        ST.modal('<form data-form="guidance" data-id="' + id + '" class="vstack gap-3"><h3>' + e(t.icon) + ' ' + e(t.name) + '</h3><p class="small mute">One tip per line. These lists are shown on the topic\'s own page.</p>' +
          field('tg-dos', 'Do', '<textarea class="form-control" id="tg-dos" name="dos" rows="5" maxlength="2000">' + e(t.dos.join('\n')) + '</textarea>') +
          field('tg-donts', 'Avoid', '<textarea class="form-control" id="tg-donts" name="donts" rows="4" maxlength="2000">' + e(t.donts.join('\n')) + '</textarea>') +
          '<div class="hrow"><span class="sp"></span><button type="button" class="btn btn-plain" data-close>Cancel</button><button class="btn btn-danger" type="submit">Save</button></div></form>');
      });
    },
    guideEdit: function (id) { ui.edit = id; show().done(function () { window.scrollTo(0, 0); $('#fg-title').trigger('focus'); }); },
    guideDel: function (id) { ST.api('DELETE', 'admin/first-aid/' + id).done(function () { if (ui.edit === id) ui.edit = null; done('First aid guide deleted'); }); },
    topicDel: function (id) { ST.api('DELETE', 'admin/content/topics/' + id).done(function () { done('Topic removed from the portal'); }); },
    numberAdd: function () { $('#numbers').append(numberRow()); $('#numbers .num-row:last input:first').trigger('focus'); },
    numberDel: function (id, el) { $(el).closest('.num-row').remove(); },
    promoDel: function (id) { ST.api('DELETE', 'admin/listings/' + id + '/promotion').done(function () { done('Promotion removed'); }); },
    adImageDel: function (slot) { ST.api('DELETE', 'admin/ads/' + slot + '/image').done(function () { done('Picture removed'); }); },
    twoSetup: function () {
      ST.api('POST', 'auth/two-factor/setup').done(function (r) {
        $('#twofa').html(r.enabled ? '<div class="hrow"><span class="pill ok">On</span><span>Two-step sign-in is on for your account.</span></div>' :
          '<form data-form="twoEnable" class="vstack gap-3"><p>1. In your authenticator app choose "enter a setup key" and type this key:</p><p class="mono fw-bold" style="overflow-wrap:anywhere">' + e(r.key) + '</p>' +
          '<div class="hrow align-items-end">' + field('tf-code', '2. Type the six-digit code the app shows', text('tf-code', 'code', '', 'required inputmode="numeric" pattern="[0-9]{6}" maxlength="6" autocomplete="one-time-code"'), 'flex:0 1 260px') + '<button class="btn btn-danger" type="submit">Turn on</button></div></form>');
      });
    }
  };
  var switches = {
    featured: function (id, on) { return ST.api('PUT', 'admin/listings/' + id + '/featured', { on: on }).done(function () { ST.toast(on ? 'Featured on the home page' : 'No longer featured'); }); },
    category: function (id, on) { return ST.api('PUT', 'admin/categories/' + id + '/active', { on: on }).done(function () { ST.toast(on ? 'Shown to the public' : 'Hidden from the public'); }); },
    section: function (id, on) { return ST.api('PUT', 'admin/content/sections/' + id, { on: on }).done(function () { ST.toast('Home page updated'); }); },
    alert: function (id, on) { return ST.api('PUT', 'admin/alerts/' + id + '/active', { on: on }).done(function () { ST.toast(on ? 'Alert is live' : 'Alert expired'); }); },
    guide: function (id, on) { return ST.api('PUT', 'admin/first-aid/' + id + '/active', { on: on }).done(function () { ST.toast(on ? 'Shown to the public' : 'Hidden from the public'); }); },
    sponsor: function (id, on) { return ST.api('PUT', 'admin/sponsors/' + id + '/active', { on: on }).done(function () { ST.toast('Sponsors updated'); }); },
    user: function (id, on) { return ST.api('PUT', 'admin/users/' + id + '/active', { on: on }).done(function () { ST.toast(on ? 'This person can sign in' : 'This person can no longer sign in'); }); }
  };
  var forms = {
    listing: function (d) {
      d.categoryId = Number(d.categoryId); d.latitude = d.latitude || null; d.longitude = d.longitude || null;
      return ui.edit ? ST.api('PUT', 'admin/listings/' + ui.edit, d).done(function () { ui.edit = null; done('Saved. The portal and app show the change now.'); })
        : ST.api('POST', 'admin/listings', d).done(function () { done('Published. It is on the portal map and in the app now.'); });
    },
    category: function (d) { return ST.api('POST', 'admin/categories', d).done(function () { done('Category added'); }); },
    hero: function (d) { return ST.api('PUT', 'admin/content/hero', d).done(function () { ST.toast('Published. The Web Portal shows the new headline now.'); }); },
    numbers: function (d, form) {
      var rows = $(form).find('.num-row').map(function () { return { label: $.trim($(this).find('[data-k="label"]').val()), number: $.trim($(this).find('[data-k="number"]').val()) }; }).get();
      return ST.api('PUT', 'admin/content/emergency-numbers', rows).done(function () { ST.toast('Emergency numbers saved'); });
    },
    guidance: function (d, form) { return ST.api('PUT', 'admin/content/topics/' + form.dataset.id + '/guidance', d).done(function () { ST.closeModal(); ST.toast('Saved. The topic page shows the new lists now.'); }); },
    guide: function (d) {
      return ui.edit ? ST.api('PUT', 'admin/first-aid/' + ui.edit, d).done(function () { ui.edit = null; done('Saved. The portal and app show the change now.'); })
        : ST.api('POST', 'admin/first-aid', d).done(function () { done('Guide added to the portal and the app'); });
    },
    topic: function (d) { return ST.api('POST', 'admin/content/topics', d).done(function () { done('Topic added to the portal'); }); },
    alert: function (d) { return ST.api('POST', 'admin/alerts', d).done(function () { done('Alert published to the portal and the app'); }); },
    fee: function (d) { return ST.api('PUT', 'admin/settings/annual-fee', { fee: Number(d.fee) }).done(function () { ST.toast('Annual fee saved'); }); },
    sponsor: function (d) { return ST.api('POST', 'admin/sponsors', d).done(function () { done('Sponsor added. Switch it on when the agreement is signed.'); }); },
    ad: function (d, form) { d.sponsorId = Number(d.sponsorId); return ST.api('PUT', 'admin/ads/' + form.dataset.slot, d).done(function () { done(d.isActive ? 'Saved. The advertisement is on the portal now.' : 'Saved. The advertisement is switched off.'); }); },
    user: function (d) { return ST.api('POST', 'admin/users', d).done(function () { done('Staff member added'); }); },
    reject: function (d, form) { return ST.api('POST', 'admin/suppliers/' + form.dataset.id + '/reject', d).done(function () { ST.closeModal(); pending = Math.max(0, pending - 1); done('Rejected. The supplier is told why.'); }); },
    twoEnable: function (d) { return ST.api('POST', 'auth/two-factor/enable', { code: String(d.code) }).done(function () { $('#twofa').html('<div class="hrow"><span class="pill ok">On</span><span>Two-step sign-in is on. You will be asked for a code each time you sign in.</span></div>'); ST.toast('Two-step sign-in is on'); }); }
  };

  $(document).on('click', '[data-act]', function () { var a = act[this.dataset.act]; if (a) { var id = this.dataset.id; a(/^\d+$/.test(id) ? Number(id) : id, this); } });
  $(document).on('change', '[data-sw]', function () {
    var el = this, s = switches[el.dataset.sw], id = el.dataset.id;
    if (s) s(/^\d+$/.test(id) ? Number(id) : id, el.checked).fail(function () { el.checked = !el.checked; });
  });
  $(document).on('change', '[data-adfile]', function () {
    var file = this.files[0];
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) { ST.toast('Choose a picture up to 2 MB.', true); this.value = ''; return; }
    var fd = new FormData(); fd.append('file', file);
    ST.api('POST', 'admin/ads/' + this.dataset.adfile + '/image', fd).done(function () { done('Picture saved'); });
  });
  $(document).on('submit', 'form[data-form]', function (ev) {
    ev.preventDefault();
    var f = forms[this.dataset.form];
    if (f) ST.busy(this, f(ST.form(this), this));
  });

  route();
})(jQuery);
