/* Web Portal: the public site. Everything shown here comes from the shared data that the admin portals edit. */
(function () {
  var ui = { page: 'home', kind: 'all', cat: '', district: '', q: '' };

  function card(l) {
    var c = Store.cat(l.cat);
    return '<button type="button" class="lcard" data-open="' + l.id + '"><span class="ic">' + c.icon + '</span>' +
      '<h3>' + esc(l.name) + '</h3><span class="meta">' + esc(c.name) + ' · ' + esc(l.town) + '</span>' +
      '<span class="row">' + (l.verified ? '<span class="pill ok">✓ Verified</span>' : '') + (l.rating ? '<span class="pill warn">★ ' + l.rating.toFixed(1) + '</span>' : '') +
      (c.kind === 'help' && l.phone && l.phone !== '-' ? '<span class="pill info num">' + esc(l.phone) + '</span>' : '') + '</span></button>';
  }

  function results() {
    var q = ui.q.toLowerCase();
    return Store.publicListings().filter(function (l) {
      var c = Store.cat(l.cat);
      if (ui.kind !== 'all' && c.kind !== ui.kind) return false;
      if (ui.cat && l.cat !== ui.cat) return false;
      if (ui.district && l.district !== ui.district) return false;
      if (q && (l.name + ' ' + l.town + ' ' + c.name).toLowerCase().indexOf(q) < 0) return false;
      return true;
    });
  }
  function resultsHtml() {
    var r = results();
    return '<p class="small mute" style="margin-top:14px">' + r.length + ' place' + (r.length === 1 ? '' : 's') + ' found</p>' +
      (r.length ? '<div class="cards">' + r.map(card).join('') + '</div>' : '<p style="margin-top:14px">Nothing matches. Clear a filter to see more places.</p>');
  }

  function header(s) {
    var nav = [['home', 'Home'], ['find', 'Find help & places'], ['donate', 'Donate']];
    return '<div class="ebar"><div class="wrap"><span class="hot">🆘 Emergency in Sri Lanka</span>' +
      s.emergency.map(function (e) { return '<span>' + esc(e.label) + ' <b>' + esc(e.number) + '</b></span>'; }).join('') + '</div></div>' +
      '<div class="phead"><div class="wrap"><button type="button" class="logo" data-act="pGo" data-page="home"><span class="mk">🛡️</span><span>Safe Travel Sri Lanka<small>Travel-safety portal · model</small></span></button>' +
      '<nav class="pnav" aria-label="Web portal">' + nav.map(function (n) { return '<button type="button" data-act="pGo" data-page="' + n[0] + '"' + (ui.page === n[0] ? ' aria-current="page"' : '') + '>' + n[1] + '</button>'; }).join('') + '</nav>' +
      '<span class="sp"></span><select class="inp" id="p-lang" style="width:auto" aria-label="Language"><option>English</option><option>සිංහල</option><option>தமிழ்</option></select>' +
      '<button type="button" class="btn" data-act="pSignIn">Sign in</button><button type="button" class="btn btn-red" data-act="pList">List your business</button></div></div>';
  }

  function home(s) {
    var sec = s.settings.sections, out = '';
    var live = s.alerts.filter(function (a) { return a.active; });
    out += '<section class="hero"><div class="wrap"><div><span class="badge">🛡️ Travel-safety service for Sri Lanka</span><h1>' + esc(s.settings.heroTitle) + '</h1><p>' + esc(s.settings.heroText) + '</p>' +
      '<div class="row" style="margin-top:22px"><button type="button" class="btn btn-red" data-act="pGo" data-page="find">Find help near you</button><button type="button" class="btn btn-ink" data-act="pApp">Open the mobile app →</button></div></div>' +
      (sec.alerts && live.length ? '<div class="live"><h4><span class="dot"></span> Live right now</h4>' + live.slice(0, 3).map(function (a) {
        return '<div class="al"><span class="dot ' + (a.level === 'red' ? '' : a.level) + '" style="margin-top:5px"></span><div><div class="k">' + esc(a.type) + ' · ' + esc(a.area) + ' · ' + esc(a.time) + '</div><b>' + esc(a.title) + '</b></div></div>';
      }).join('') + '</div>' : '<span></span>') + '</div></section>';
    out += '<div class="srcs"><div class="wrap"><span class="eyebrow" style="color:var(--mute)">Alerts & data from</span><span>Disaster Management Centre</span><span>NBRO</span><span>Dept. of Meteorology</span><span>Ministry of Health</span></div></div>';

    if (sec.map) {
      var help = Store.publicListings().filter(function (l) { return Store.cat(l.cat).kind === 'help'; });
      out += '<section class="sec"><div class="wrap split"><div><span class="eyebrow">Find help</span><h2>Banks, SOS points and police stations on one map</h2>' +
        '<p class="lead">Every help point is kept up to date by the admin team. Tap a category to see the list, or tap a pin.</p><div class="row" style="margin-top:16px">' +
        s.categories.filter(function (c) { return c.kind === 'help' && c.on; }).map(function (c) { return '<button type="button" class="chip" data-act="pCat" data-cat="' + c.id + '">' + c.icon + ' ' + esc(c.name) + '</button>'; }).join('') +
        '</div></div>' + mapHtml(help) + '</div></section>';
    }
    if (sec.businesses) {
      var biz = Store.publicListings().filter(function (l) { return Store.cat(l.cat).kind === 'business'; });
      biz.sort(function (a, b) { return (b.featured ? 1 : 0) - (a.featured ? 1 : 0) || b.rating - a.rating; });
      out += '<section class="sec" style="background:var(--soft)"><div class="wrap"><span class="eyebrow">Verified businesses</span><h2>Places travellers can trust</h2>' +
        '<p class="lead">Every business here is registered, checked by the admin team and renewed each year.</p><div class="cards">' + biz.slice(0, 6).map(card).join('') + '</div>' +
        '<div class="row" style="margin-top:18px"><button type="button" class="btn btn-ink" data-act="pBiz">See all businesses →</button></div></div></section>';
    }
    if (sec.topics) {
      out += '<section class="sec"><div class="wrap"><span class="eyebrow">Safety topics</span><h2>Know before you go</h2><div class="cards">' +
        s.topics.map(function (t) { return '<div class="tcard tone-' + t.tone + '"><span class="ic">' + t.icon + '</span><h3>' + esc(t.name) + '</h3><p class="small">' + esc(t.text) + '</p></div>'; }).join('') + '</div></div></section>';
    }
    out += '<section class="sec" style="padding-top:0"><div class="wrap duo"><div class="big" style="background:var(--t-blue)"><span style="font-size:34px">🏨</span><h3>Own a business?</h3><p>Register, upload your documents and pay ' + lkr(s.settings.annualFee) + ' a year to appear on the map and in the app.</p><button type="button" class="btn btn-ink" data-act="pList">List your business →</button></div>' +
      (sec.donate ? '<div class="big" style="background:var(--t-rose)"><span style="font-size:34px">❤️</span><h3>Support disaster response</h3><p>Donations go straight to the Sri Lanka Red Cross Society account for flood relief and first aid training.</p><button type="button" class="btn btn-red" data-act="pGo" data-page="donate">Donate →</button></div>' : '') + '</div></section>';
    var sp = s.sponsors.filter(function (x) { return x.on; });
    if (sec.sponsors && sp.length) {
      out += '<section class="sec" style="padding-top:0"><div class="wrap"><span class="eyebrow" style="color:var(--mute)">Proposed sponsors</span><div class="spons">' +
        sp.map(function (x) { return '<div class="spon"><b>' + esc(x.name) + '</b><span class="small mute">' + esc(x.role) + '</span></div>'; }).join('') + '</div></div></section>';
    }
    return out;
  }

  function find(s) {
    var districts = {}; Store.publicListings().forEach(function (l) { districts[l.district] = 1; });
    var kinds = [['all', 'All'], ['help', 'Help points'], ['business', 'Businesses']];
    return '<section class="sec"><div class="wrap"><span class="eyebrow">Directory</span><h2>Find help and trusted places</h2><div class="filters">' +
      kinds.map(function (k) { return '<button type="button" class="chip' + (ui.kind === k[0] ? ' on' : '') + '" data-act="pKind" data-kind="' + k[0] + '">' + k[1] + '</button>'; }).join('') +
      '<select class="inp" id="p-cat" style="width:auto" data-on="pFilter" data-k="cat" aria-label="Category"><option value="">All categories</option>' +
      s.categories.filter(function (c) { return c.on && (ui.kind === 'all' || c.kind === ui.kind); }).map(function (c) { return '<option value="' + c.id + '"' + (ui.cat === c.id ? ' selected' : '') + '>' + c.icon + ' ' + esc(c.name) + '</option>'; }).join('') + '</select>' +
      '<select class="inp" id="p-district" style="width:auto" data-on="pFilter" data-k="district" aria-label="District"><option value="">All districts</option>' +
      Object.keys(districts).sort().map(function (d) { return '<option' + (ui.district === d ? ' selected' : '') + '>' + esc(d) + '</option>'; }).join('') + '</select>' +
      '<input class="inp" id="p-q" type="search" placeholder="Search by name or town" value="' + esc(ui.q) + '" data-on="pSearch" aria-label="Search">' +
      '</div><div id="p-results">' + resultsHtml() + '</div></div></section>';
  }

  var amount = 2500;
  function donate() {
    return '<section class="sec"><div class="wrap split"><div><span class="eyebrow">Donate</span><h2>Help the Red Cross reach people faster</h2>' +
      '<p class="lead">Your donation is paid through the payment gateway straight into the Sri Lanka Red Cross Society account. You get a receipt by email.</p></div>' +
      '<form class="panel" data-form="pDonate" style="margin-top:0"><div class="body" style="display:flex;flex-direction:column;gap:14px"><div class="amounts">' +
      [1000, 2500, 5000, 10000].map(function (a) { return '<button type="button" class="chip' + (a === amount ? ' on' : '') + '" data-act="pAmount" data-a="' + a + '">' + lkr(a) + '</button>'; }).join('') + '</div>' +
      '<div class="field"><label for="d-name">Name (optional)</label><input id="d-name" name="name" autocomplete="off"></div>' +
      '<div class="field"><label for="d-email">Email for the receipt</label><input id="d-email" name="email" type="email" placeholder="you@example.com"></div>' +
      '<button class="btn btn-red" type="submit">Donate ' + lkr(amount) + '</button><p class="small mute">Model only. No payment is taken.</p></div></form></div></section>';
  }

  function footer(s) {
    return '<footer class="pfoot"><div class="wrap"><div><b style="color:#fff;font-family:var(--display);font-size:17px">Safe Travel Sri Lanka</b><p style="max-width:34ch;margin-top:6px">A travel-safety service for visitors and residents. Model with sample data.</p></div>' +
      '<table><tbody>' + s.emergency.map(function (e) { return '<tr><td>' + esc(e.label) + '</td><td class="num">' + esc(e.number) + '</td></tr>'; }).join('') + '</tbody></table>' +
      '<div>Developed by <b style="color:#fff">Evokes Digital Worldwide</b></div></div></footer>';
  }

  Apps.portal = { render: function () { var s = Store.get(); return header(s) + (ui.page === 'find' ? find(s) : ui.page === 'donate' ? donate() : home(s)) + footer(s); } };

  Acts.pGo = function (el) { ui.page = el.dataset.page; render(); window.scrollTo(0, 0); };
  Acts.pCat = function (el) { ui.page = 'find'; ui.kind = 'help'; ui.cat = el.dataset.cat; ui.q = ''; ui.district = ''; render(); window.scrollTo(0, 0); };
  Acts.pBiz = function () { ui.page = 'find'; ui.kind = 'business'; ui.cat = ''; ui.q = ''; ui.district = ''; render(); window.scrollTo(0, 0); };
  Acts.pKind = function (el) { ui.kind = el.dataset.kind; ui.cat = ''; render(); };
  Acts.pFilter = function (el) { ui[el.dataset.k] = el.value; render(); };
  Acts.pSearch = function (el) { ui.q = el.value; document.getElementById('p-results').innerHTML = resultsHtml(); };
  Acts.pAmount = function (el) { amount = Number(el.dataset.a); render(); };
  Acts.pSignIn = function () { toast('Sign-in opens here. Travellers use one account on the web and in the app.'); };
  Acts.pList = function () { Acts.sRegister(); go('supplier'); };
  Acts.pApp = function () { go('mobile'); };
  Forms.pDonate = function (d) {
    var s = Store.get();
    s.payments.unshift({ id: s.payments[0].id + 1, date: new Date().toISOString().slice(0, 10), kind: 'Donation', who: d.name || 'Anonymous donor', amount: amount, status: 'Paid' });
    Store.save(); toast('Thank you. ' + lkr(amount) + ' recorded. See it under Payments in the Admin Portal.');
  };
})();
