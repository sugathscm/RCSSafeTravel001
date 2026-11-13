/* Admin Portal (Internal): staff control everything the Web Portal and Mobile App show. */
(function () {
  var ui = { page: 'dash', kind: 'help', edit: null };
  var ME = 'Super admin';
  var DC = { Colombo: [6.93, 79.86], Gampaha: [7.09, 79.99], Kandy: [7.29, 80.63], Galle: [6.05, 80.22], Matara: [5.95, 80.54], Badulla: [6.99, 81.05], Matale: [7.47, 80.62], 'Nuwara Eliya': [6.95, 80.79], Hambantota: [6.12, 81.12], Anuradhapura: [8.31, 80.40], Trincomalee: [8.57, 81.23], Jaffna: [9.66, 80.02] };

  function st(status) {
    var m = { published: ['ok', 'Live'], pending: ['warn', 'Awaiting review'], unpaid: ['info', 'Awaiting payment'], lapsed: ['bad', 'Lapsed'], hidden: ['', 'Hidden'], rejected: ['bad', 'Rejected'], approved: ['ok', 'Approved'] };
    var x = m[status] || ['', status];
    return '<span class="pill ' + x[0] + '">' + x[1] + '</span>';
  }
  function head(t, sub) { return '<h2>' + t + '</h2><p class="sub">' + sub + '</p>'; }
  function panel(title, body, extra) { return '<section class="panel"><header><h3>' + title + '</h3><span class="sp"></span>' + (extra || '') + '</header>' + body + '</section>'; }
  function table(cols, rows) { return '<div class="tw"><table class="t"><thead><tr>' + cols.map(function (c) { return '<th>' + c + '</th>'; }).join('') + '</tr></thead><tbody>' + (rows.join('') || '<tr><td colspan="' + cols.length + '" class="mute">Nothing here yet.</td></tr>') + '</tbody></table></div>'; }
  function sw(act, id, on, label) { return '<label class="sw"><input type="checkbox" id="sw-' + act + '-' + id + '" data-on="' + act + '" data-id="' + id + '"' + (on ? ' checked' : '') + '>' + (label || '') + '</label>'; }
  function catOptions(kind, sel) { return Store.get().categories.filter(function (c) { return !kind || c.kind === kind; }).map(function (c) { return '<option value="' + c.id + '"' + (sel === c.id ? ' selected' : '') + '>' + c.icon + ' ' + esc(c.name) + '</option>'; }).join(''); }
  function find(arr, id) { return arr.filter(function (x) { return String(x.id) === String(id); })[0]; }

  function dash(s) {
    var pend = s.suppliers.filter(function (x) { return x.status === 'pending'; });
    var month = s.payments.filter(function (p) { return p.status === 'Paid'; }).reduce(function (a, p) { return a + p.amount; }, 0);
    var lapsed = s.listings.filter(function (l) { return l.status === 'lapsed'; });
    return head('Dashboard', 'What needs attention today, across suppliers, listings, alerts and payments.') +
      '<div class="stats"><div class="stat"><b>' + Store.publicListings().length + '</b><span>Listings live on the portal and app</span></div><div class="stat"><b>' + pend.length + '</b><span>Suppliers awaiting review</span></div>' +
      '<div class="stat"><b>' + s.alerts.filter(function (a) { return a.active; }).length + '</b><span>Active alerts</span></div><div class="stat"><b>' + lkr(month) + '</b><span>Collected this month</span></div></div>' +
      panel('Needs attention', table(['Item', 'Why', ''], pend.map(function (x) {
        return '<tr><td><b>' + esc(x.business) + '</b></td><td>New supplier, ' + x.docs.length + ' document' + (x.docs.length === 1 ? '' : 's') + ' to check</td><td><div class="acts"><button type="button" class="btn btn-sm btn-ink" data-act="aApprove" data-id="' + x.id + '">Approve</button><button type="button" class="btn btn-sm" data-act="aReject" data-id="' + x.id + '">Reject</button></div></td></tr>';
      }).concat(lapsed.map(function (l) {
        return '<tr><td><b>' + esc(l.name) + '</b></td><td>Annual fee not renewed, hidden from the public</td><td><div class="acts"><button type="button" class="btn btn-sm" data-act="aRemind" data-id="' + l.supplierId + '">Send reminder</button></div></td></tr>';
      })))) +
      '<div class="grid2">' + panel('New supplier registrations, last 8 weeks', '<div class="body"><div class="bars" role="img" aria-label="Registrations rising from 4 to 23 a week">' + [4, 6, 5, 9, 12, 11, 17, 23].map(function (v) { return '<i style="height:' + (v / 23 * 100) + '%" title="' + v + '"></i>'; }).join('') + '</div><p class="small mute" style="margin-top:8px">Sample figures</p></div>') +
      panel('Latest activity', table(['When', 'Who', 'What'], s.audit.slice(0, 4).map(function (a) { return '<tr><td class="small num" style="white-space:nowrap">' + esc(a.time) + '</td><td class="small">' + esc(a.who) + '</td><td class="small">' + esc(a.what) + '</td></tr>'; }))) + '</div>';
  }

  function suppliers(s) {
    return head('Suppliers', 'Businesses that registered through the supplier portal. Approve them after checking their documents. A listing goes live once the annual fee is paid.') +
      panel('All suppliers', table(['Business', 'Contact', 'Documents', 'Status', 'Paid until', ''], s.suppliers.map(function (x) {
        var l = Store.listingOf(x.id) || {};
        var acts = x.status === 'pending' ? '<button type="button" class="btn btn-sm btn-ink" data-act="aApprove" data-id="' + x.id + '">Approve</button><button type="button" class="btn btn-sm" data-act="aReject" data-id="' + x.id + '">Reject</button>' :
          x.status === 'approved' ? '<button type="button" class="btn btn-sm" data-act="aRemind" data-id="' + x.id + '">Send reminder</button>' + (l.id ? '<button type="button" class="btn btn-sm" data-open="' + l.id + '">View</button>' : '') : '';
        return '<tr><td><b>' + esc(x.business) + '</b><span class="small mute">' + esc(Store.cat(l.cat).name) + ' · ' + esc(l.town || '') + '</span></td><td>' + esc(x.contact) + '<span class="small mute">' + esc(x.phone) + '</span></td>' +
          '<td>' + x.docs.map(function (d) { return '<span class="pill ' + (d.status === 'verified' ? 'ok' : 'warn') + '" title="' + esc(d.status) + '">' + esc(d.name) + '</span>'; }).join(' ') + '</td>' +
          '<td>' + st(x.status === 'approved' ? (l.status || 'approved') : x.status) + '</td><td class="num">' + (x.paidUntil || '<span class="mute">Not paid</span>') + '</td><td><div class="acts">' + acts + '</div></td></tr>';
      })));
  }

  function listings(s) {
    var list = s.listings.filter(function (l) { return Store.cat(l.cat).kind === ui.kind; });
    var e = ui.edit ? find(s.listings, ui.edit) : null;
    var form = ui.kind === 'help' || e ? '<form class="panel" data-form="aListing"><header><h3>' + (e ? 'Edit ' + esc(e.name) : 'Add a help point') + '</h3></header><div class="body"><div class="grid2">' +
      '<div class="field"><label for="al-name">Name</label><input id="al-name" name="name" required value="' + esc(e ? e.name : '') + '"></div>' +
      '<div class="field"><label for="al-cat">Category</label><select id="al-cat" name="cat">' + catOptions(e ? Store.cat(e.cat).kind : 'help', e ? e.cat : 'bank') + '</select></div>' +
      '<div class="field"><label for="al-town">Town</label><input id="al-town" name="town" required value="' + esc(e ? e.town : '') + '"></div>' +
      '<div class="field"><label for="al-district">District</label><select id="al-district" name="district">' + Object.keys(DC).map(function (d) { return '<option' + (e && e.district === d ? ' selected' : '') + '>' + d + '</option>'; }).join('') + '</select></div>' +
      '<div class="field"><label for="al-phone">Phone</label><input id="al-phone" name="phone" value="' + esc(e ? e.phone : '') + '"></div>' +
      '<div class="field"><label for="al-hours">Opening hours</label><input id="al-hours" name="hours" value="' + esc(e ? e.hours : '') + '"></div></div>' +
      '<div class="row" style="margin-top:14px"><button class="btn btn-red" type="submit">' + (e ? 'Save changes' : 'Publish help point') + '</button>' + (e ? '<button type="button" class="btn" data-act="aEditCancel">Cancel</button>' : '') + '</div></div></form>' : '';
    return head('Listings', 'Help points such as banks, SOS points and police stations are added by staff. Business listings come from suppliers. Both appear on the portal map and in the app.') +
      '<div class="row" style="margin-top:16px"><button type="button" class="chip' + (ui.kind === 'help' ? ' on' : '') + '" data-act="aKind" data-kind="help">Help points</button><button type="button" class="chip' + (ui.kind === 'business' ? ' on' : '') + '" data-act="aKind" data-kind="business">Business listings</button></div>' + form +
      panel(ui.kind === 'help' ? 'Help points' : 'Business listings', table(['Name', 'Category', 'Town', 'Status', ui.kind === 'business' ? 'Featured' : 'Phone', ''], list.map(function (l) {
        var c = Store.cat(l.cat);
        var toggle = l.status === 'published' ? '<button type="button" class="btn btn-sm" data-act="aHide" data-id="' + l.id + '">Hide</button>' : l.status === 'hidden' ? '<button type="button" class="btn btn-sm" data-act="aShow" data-id="' + l.id + '">Show</button>' : '';
        return '<tr><td><b>' + esc(l.name) + '</b></td><td>' + c.icon + ' ' + esc(c.name) + '</td><td>' + esc(l.town) + '</td><td>' + st(l.status) + '</td><td class="num">' + (ui.kind === 'business' ? sw('aFeature', l.id, l.featured) : esc(l.phone)) + '</td>' +
          '<td><div class="acts"><button type="button" class="btn btn-sm" data-act="aEdit" data-id="' + l.id + '">Edit</button>' + toggle + (ui.kind === 'help' ? '<button type="button" class="btn btn-sm" data-act="aDelete" data-id="' + l.id + '">Delete</button>' : '') + '</div></td></tr>';
      })));
  }

  function categories(s) {
    return head('Categories', 'Turn a category off and it disappears from the portal filters, the map and the app. Add new ones whenever you need them.') +
      '<form class="panel" data-form="aCategory"><header><h3>Add a category</h3></header><div class="body"><div class="row"><div class="field" style="flex:2 1 200px"><label for="ac-name">Name</label><input id="ac-name" name="name" required placeholder="Tourist information centres"></div>' +
      '<div class="field" style="flex:0 1 90px"><label for="ac-icon">Icon</label><input id="ac-icon" name="icon" value="ℹ️" maxlength="4"></div><div class="field" style="flex:1 1 150px"><label for="ac-kind">Type</label><select id="ac-kind" name="kind"><option value="help">Help point</option><option value="business">Business</option></select></div>' +
      '<button class="btn btn-red" type="submit" style="align-self:flex-end">Add category</button></div></div></form>' +
      panel('All categories', table(['Category', 'Type', 'Listings', 'Shown to the public'], s.categories.map(function (c) {
        return '<tr><td><b>' + c.icon + ' ' + esc(c.name) + '</b></td><td>' + (c.kind === 'help' ? 'Help point' : 'Business') + '</td><td class="num">' + s.listings.filter(function (l) { return l.cat === c.id; }).length + '</td><td>' + sw('aCatOn', c.id, c.on) + '</td></tr>';
      })));
  }

  function content(s) {
    var sec = s.settings.sections, labels = { alerts: 'Live alerts panel', map: 'Help points map', businesses: 'Verified businesses', topics: 'Safety topics', donate: 'Donate card', sponsors: 'Sponsors strip' };
    return head('Web Portal content', 'The public site has no fixed pages. The headline, sections, emergency numbers and topics below are what it shows.') +
      '<form class="panel" data-form="aHero"><header><h3>Home page headline</h3></header><div class="body" style="display:flex;flex-direction:column;gap:12px"><div class="field"><label for="ah-title">Headline</label><input id="ah-title" name="heroTitle" required value="' + esc(s.settings.heroTitle) + '"></div>' +
      '<div class="field"><label for="ah-text">Text under the headline</label><textarea id="ah-text" name="heroText" rows="2">' + esc(s.settings.heroText) + '</textarea></div><div><button class="btn btn-red" type="submit">Publish to the portal</button></div></div></form>' +
      panel('Home page sections', '<div class="body row" style="gap:18px">' + Object.keys(labels).map(function (k) { return sw('aSection', k, sec[k], labels[k]); }).join('') + '</div>') +
      '<form class="panel" data-form="aNumbers"><header><h3>Emergency numbers</h3><span class="sp"></span><span class="small mute">Shown in the top bar, the footer and the app</span></header><div class="body"><div class="grid2">' +
      s.emergency.map(function (e, i) { return '<div class="row" style="flex-wrap:nowrap"><input class="inp" id="an-l' + i + '" name="l' + i + '" value="' + esc(e.label) + '" aria-label="Label ' + (i + 1) + '"><input class="inp num" id="an-n' + i + '" name="n' + i + '" value="' + esc(e.number) + '" style="max-width:110px" aria-label="Number ' + (i + 1) + '"></div>'; }).join('') +
      '</div><div style="margin-top:12px"><button class="btn btn-red" type="submit">Save numbers</button></div></div></form>' +
      '<form class="panel" data-form="aTopic"><header><h3>Safety topics</h3></header>' + table(['Topic', 'Summary', ''], s.topics.map(function (t, i) { return '<tr><td><b>' + t.icon + ' ' + esc(t.name) + '</b></td><td class="small">' + esc(t.text) + '</td><td><div class="acts"><button type="button" class="btn btn-sm" data-act="aTopicDel" data-id="' + i + '">Remove</button></div></td></tr>'; })) +
      '<div class="body row"><div class="field" style="flex:1 1 180px"><label for="at-name">New topic</label><input id="at-name" name="name" required></div><div class="field" style="flex:2 1 240px"><label for="at-text">Summary</label><input id="at-text" name="text" required></div><button class="btn btn-red" type="submit" style="align-self:flex-end">Add topic</button></div></form>';
  }

  function alerts(s) {
    return head('Alerts', 'Publish a warning by area. It appears on the portal home page and is pushed to phones in that area.') +
      '<form class="panel" data-form="aAlert"><header><h3>Publish an alert</h3></header><div class="body"><div class="grid2"><div class="field"><label for="aa-type">Type</label><select id="aa-type" name="type"><option>Flood warning</option><option>Landslide watch</option><option>Red flag</option><option>Road closure</option><option>Storm warning</option><option>Health notice</option></select></div>' +
      '<div class="field"><label for="aa-level">Level</label><select id="aa-level" name="level"><option value="red">Red: act now</option><option value="amber">Amber: be ready</option><option value="blue">Blue: be aware</option></select></div>' +
      '<div class="field"><label for="aa-area">Area</label><input id="aa-area" name="area" required placeholder="Nuwara Eliya"></div><div class="field"><label for="aa-title">Message</label><input id="aa-title" name="title" required placeholder="Strong winds expected tonight"></div></div>' +
      '<div style="margin-top:14px"><button class="btn btn-red" type="submit">Publish alert</button></div></div></form>' +
      panel('All alerts', table(['Alert', 'Area', 'Source', 'Sent', 'Active'], s.alerts.map(function (a) {
        return '<tr><td><span class="dot ' + (a.level === 'red' ? '' : a.level) + '"></span> <b>' + esc(a.title) + '</b><span class="small mute">' + esc(a.type) + '</span></td><td>' + esc(a.area) + '</td><td>' + esc(a.source) + '</td><td class="small">' + esc(a.time) + '</td><td>' + sw('aAlertOn', a.id, a.active) + '</td></tr>';
      })));
  }

  function payments(s) {
    function sum(k) { return s.payments.filter(function (p) { return p.kind === k && p.status === 'Paid'; }).reduce(function (a, p) { return a + p.amount; }, 0); }
    return head('Payments', 'Donations and supplier annual fees, paid through the payment gateway into the Red Cross account.') +
      '<div class="stats"><div class="stat"><b>' + lkr(sum('Donation')) + '</b><span>Donations</span></div><div class="stat"><b>' + lkr(sum('Supplier fee')) + '</b><span>Supplier annual fees</span></div><div class="stat"><b>' + s.payments.filter(function (p) { return p.status === 'Failed'; }).length + '</b><span>Failed payments to follow up</span></div></div>' +
      '<form class="panel" data-form="aFee"><header><h3>Supplier annual fee</h3></header><div class="body row"><div class="field" style="flex:0 1 200px"><label for="af-fee">Fee per listing, per year (LKR)</label><input id="af-fee" name="fee" type="number" min="0" step="100" value="' + s.settings.annualFee + '"></div><button class="btn btn-red" type="submit" style="align-self:flex-end">Save fee</button><span class="small mute" style="align-self:flex-end">Reminders go out 30, 14 and 7 days before renewal, and on the day.</span></div></form>' +
      panel('Transactions', table(['No.', 'Date', 'Type', 'From', 'Amount', 'Status'], s.payments.map(function (p) {
        return '<tr><td class="num">' + p.id + '</td><td class="num">' + p.date + '</td><td>' + p.kind + '</td><td>' + esc(p.who) + '</td><td class="num">' + lkr(p.amount) + '</td><td><span class="pill ' + (p.status === 'Paid' ? 'ok' : 'bad') + '">' + p.status + '</span></td></tr>';
      })));
  }

  function sponsors(s) {
    return head('Sponsors', 'Sponsors shown on the portal. Names appear only after each agreement is signed.') +
      '<form class="panel" data-form="aSponsor"><header><h3>Add a sponsor</h3></header><div class="body row"><div class="field" style="flex:1 1 180px"><label for="as-name">Name</label><input id="as-name" name="name" required></div><div class="field" style="flex:1 1 180px"><label for="as-role">Role</label><input id="as-role" name="role" required placeholder="Insurance partner"></div><button class="btn btn-red" type="submit" style="align-self:flex-end">Add sponsor</button></div></form>' +
      panel('Sponsors', table(['Sponsor', 'Role', 'Shown on the portal'], s.sponsors.map(function (x) { return '<tr><td><b>' + esc(x.name) + '</b></td><td>' + esc(x.role) + '</td><td>' + sw('aSponsorOn', x.id, x.on) + '</td></tr>'; })));
  }

  function users(s) {
    return head('Users and roles', 'Each staff member sees only the pages their role allows. Every admin signs in with a second step.') +
      panel('Staff', table(['Name', 'Role', 'Two-step sign-in', 'Active'], s.users.map(function (u, i) {
        return '<tr><td><b>' + esc(u.name) + '</b><span class="small mute">' + esc(u.email) + '</span></td><td>' + esc(u.role) + '</td><td><span class="pill ' + (u.twoFactor ? 'ok">On' : 'bad">Off') + '</span></td><td>' + sw('aUserOn', i, u.active) + '</td></tr>';
      }))) +
      panel('What each role can do', table(['Role', 'Can do'], [['Super admin', 'Everything, including staff accounts and settings'], ['Content reviewer', 'Edit and publish portal content, numbers and topics'], ['Alerts officer', 'Publish and expire alerts'], ['Supplier manager', 'Approve suppliers and manage listings and categories'], ['Finance officer', 'See payments and reports, set the annual fee'], ['Call centre agent', 'Look up suppliers and listings, help with registrations']].map(function (r) { return '<tr><td><b>' + r[0] + '</b></td><td>' + r[1] + '</td></tr>'; })));
  }

  function system() {
    var api = [['GET', '/api/v1/listings', 'Listings by category, district, search and distance', 'Public'], ['GET', '/api/v1/categories', 'Categories that are switched on', 'Public'], ['GET', '/api/v1/alerts', 'Active alerts by area', 'Public'], ['GET', '/api/v1/content/home', 'Headline, sections, topics, emergency numbers', 'Public'],
      ['POST', '/api/v1/auth/login', 'Sign in, returns a short-lived token', 'Public, rate limited'], ['POST', '/api/v1/suppliers', 'Register a business', 'Supplier'], ['PUT', '/api/v1/suppliers/me/listing', 'Update own listing', 'Supplier'], ['POST', '/api/v1/payments/checkout', 'Start a donation or annual fee payment', 'Signed in'],
      ['POST', '/api/v1/admin/alerts', 'Publish an alert', 'Alerts officer'], ['POST', '/api/v1/admin/suppliers/{id}/approve', 'Approve a supplier', 'Supplier manager']];
    var sec = ['Two-step sign-in required for all staff', 'Sign-in locks after 5 wrong attempts', 'Short-lived tokens with refresh, revoked on sign-out', 'Rate limits on every public endpoint', 'Every admin change written to the audit log', 'Listings and content cached so alert peaks do not slow the site'];
    return head('Security and API', 'One REST API serves the Web Portal, both admin portals and the Android and iOS apps.') +
      panel('Security rules', '<div class="body" style="display:grid;gap:8px">' + sec.map(function (x) { return '<div class="row"><span class="pill ok">On</span><span>' + x + '</span></div>'; }).join('') + '</div>') +
      panel('REST API', table(['Method', 'Endpoint', 'What it does', 'Who can call it'], api.map(function (r) { return '<tr><td><span class="pill ' + (r[0] === 'GET' ? 'info' : 'warn') + '">' + r[0] + '</span></td><td style="font-family:var(--mono);font-size:13px;white-space:nowrap">' + r[1] + '</td><td>' + r[2] + '</td><td>' + r[3] + '</td></tr>'; })));
  }

  function audit(s) { return head('Audit log', 'Who changed what, and when. Entries cannot be edited or deleted.') + panel('All activity', table(['When', 'Who', 'What'], s.audit.map(function (a) { return '<tr><td class="num" style="white-space:nowrap">' + esc(a.time) + '</td><td>' + esc(a.who) + '</td><td>' + esc(a.what) + '</td></tr>'; }))); }

  var pages = [['dash', '📊', 'Dashboard', dash], ['suppliers', '🏪', 'Suppliers', suppliers], ['listings', '📍', 'Listings', listings], ['categories', '🗂️', 'Categories', categories], ['content', '🖥️', 'Web Portal content', content], ['alerts', '🔔', 'Alerts', alerts], ['payments', '💳', 'Payments', payments], ['sponsors', '🤝', 'Sponsors', sponsors], ['users', '👥', 'Users and roles', users], ['system', '🔐', 'Security and API', system], ['audit', '🧾', 'Audit log', audit]];

  Apps.admin = {
    render: function () {
      var s = Store.get(), pend = s.suppliers.filter(function (x) { return x.status === 'pending'; }).length;
      var pg = pages.filter(function (p) { return p[0] === ui.page; })[0] || pages[0];
      return '<div class="adm"><nav class="side" aria-label="Admin Portal"><div class="who"><b>Admin Portal</b>Internal · signed in as ' + ME + '</div>' +
        pages.map(function (p) { return '<button type="button" data-act="aGo" data-page="' + p[0] + '"' + (p[0] === pg[0] ? ' aria-current="page"' : '') + '><span>' + p[1] + '</span>' + p[2] + (p[0] === 'suppliers' && pend ? '<span class="cnt">' + pend + '</span>' : '') + '</button>'; }).join('') +
        '</nav><div class="main">' + pg[3](s) + '</div></div>';
    }
  };

  function done(msg, what) { if (what) Store.log(ME, what); Store.save(); toast(msg); }
  Acts.aGo = function (el) { ui.page = el.dataset.page; ui.edit = null; render(); window.scrollTo(0, 0); };
  Acts.aKind = function (el) { ui.kind = el.dataset.kind; ui.edit = null; render(); };
  Acts.aApprove = function (el) {
    var s = Store.get(), x = Store.supplier(Number(el.dataset.id)), l = Store.listingOf(x.id);
    x.status = 'approved'; x.docs.forEach(function (d) { d.status = 'verified'; });
    if (l) { l.verified = true; l.status = x.paidUntil ? 'published' : 'unpaid'; }
    done('Approved. ' + x.business + ' can now pay the annual fee in the supplier portal.', 'Approved supplier "' + x.business + '"');
  };
  Acts.aReject = function (el) { var x = Store.supplier(Number(el.dataset.id)), l = Store.listingOf(x.id); x.status = 'rejected'; if (l) l.status = 'rejected'; done('Rejected. The supplier is told why by email.', 'Rejected supplier "' + x.business + '"'); };
  Acts.aRemind = function (el) { var x = Store.supplier(Number(el.dataset.id)); done('Renewal reminder sent to ' + x.business + ' by email and SMS', 'Sent renewal reminder to "' + x.business + '"'); };
  Acts.aHide = function (el) { var l = find(Store.get().listings, el.dataset.id); l.status = 'hidden'; done('Hidden from the portal and app', 'Hid listing "' + l.name + '"'); };
  Acts.aShow = function (el) { var l = find(Store.get().listings, el.dataset.id); l.status = 'published'; done('Live on the portal and app', 'Showed listing "' + l.name + '"'); };
  Acts.aDelete = function (el) { var s = Store.get(), l = find(s.listings, el.dataset.id); s.listings = s.listings.filter(function (x) { return x !== l; }); done('Help point deleted', 'Deleted help point "' + l.name + '"'); };
  Acts.aEdit = function (el) { ui.edit = Number(el.dataset.id); render(); window.scrollTo(0, 0); };
  Acts.aEditCancel = function () { ui.edit = null; render(); };
  Acts.aFeature = function (el) { var l = find(Store.get().listings, el.dataset.id); l.featured = el.checked; done(l.featured ? 'Featured on the home page' : 'No longer featured', (l.featured ? 'Featured' : 'Unfeatured') + ' "' + l.name + '"'); };
  Acts.aCatOn = function (el) { var c = find(Store.get().categories, el.dataset.id); c.on = el.checked; done(c.name + (c.on ? ' is shown to the public' : ' is hidden from the public'), 'Turned category "' + c.name + '" ' + (c.on ? 'on' : 'off')); };
  Acts.aSection = function (el) { Store.get().settings.sections[el.dataset.id] = el.checked; done('Home page updated', 'Turned home page section "' + el.dataset.id + '" ' + (el.checked ? 'on' : 'off')); };
  Acts.aAlertOn = function (el) { var a = find(Store.get().alerts, el.dataset.id); a.active = el.checked; done(a.active ? 'Alert is live' : 'Alert expired', (a.active ? 'Reactivated' : 'Expired') + ' alert "' + a.title + '"'); };
  Acts.aSponsorOn = function (el) { var x = find(Store.get().sponsors, el.dataset.id); x.on = el.checked; done('Sponsors updated', 'Turned sponsor "' + x.name + '" ' + (x.on ? 'on' : 'off')); };
  Acts.aUserOn = function (el) { var u = Store.get().users[Number(el.dataset.id)]; u.active = el.checked; done(u.name + (u.active ? ' can sign in' : ' can no longer sign in'), (u.active ? 'Enabled' : 'Disabled') + ' user "' + u.name + '"'); };
  Acts.aTopicDel = function (el) { var s = Store.get(), t = s.topics.splice(Number(el.dataset.id), 1)[0]; done('Topic removed from the portal', 'Removed topic "' + t.name + '"'); };

  Forms.aListing = function (d) {
    var s = Store.get(), e = ui.edit ? find(s.listings, ui.edit) : null;
    if (e) { e.name = d.name; e.cat = d.cat; e.town = d.town; e.district = d.district; e.phone = d.phone; e.hours = d.hours; ui.edit = null; done('Saved. The portal and app show the change now.', 'Edited listing "' + e.name + '"'); return; }
    var c = DC[d.district] || DC.Colombo;
    s.listings.push({ id: Store.id(), cat: d.cat, name: d.name, town: d.town, district: d.district, phone: d.phone || '-', hours: d.hours || 'Not given', lat: c[0] + (Math.random() - 0.5) * 0.12, lng: c[1] + (Math.random() - 0.5) * 0.12, status: 'published', desc: '' });
    done('Published. Open the Web Portal or Mobile App to see it.', 'Added help point "' + d.name + '"');
  };
  Forms.aCategory = function (d) { Store.get().categories.push({ id: 'c' + Store.id(), kind: d.kind, name: d.name, icon: d.icon || '📍', on: true }); done('Category added', 'Added category "' + d.name + '"'); };
  Forms.aHero = function (d) { var st2 = Store.get().settings; st2.heroTitle = d.heroTitle; st2.heroText = d.heroText; done('Published. Open the Web Portal to see the new headline.', 'Changed the home page headline'); };
  Forms.aNumbers = function (d) { Store.get().emergency.forEach(function (e, i) { e.label = d['l' + i] || e.label; e.number = d['n' + i] || e.number; }); done('Emergency numbers saved', 'Edited emergency numbers'); };
  Forms.aTopic = function (d) { Store.get().topics.push({ name: d.name, text: d.text, icon: '📘', tone: 'blue' }); done('Topic added to the portal', 'Added topic "' + d.name + '"'); };
  Forms.aAlert = function (d) { var s = Store.get(); s.alerts.unshift({ id: Store.id(), level: d.level, type: d.type, area: d.area, title: d.title, source: 'Alert desk', time: 'Just now', active: true }); done('Alert published to the portal and the app', 'Published alert "' + d.title + '"'); };
  Forms.aFee = function (d) { Store.get().settings.annualFee = Number(d.fee) || 0; done('Annual fee saved', 'Set the supplier annual fee to ' + lkr(d.fee)); };
  Forms.aSponsor = function (d) { Store.get().sponsors.push({ id: Store.id(), name: d.name, role: d.role, on: true }); done('Sponsor added', 'Added sponsor "' + d.name + '"'); };
})();
