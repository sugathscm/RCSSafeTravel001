/* Mobile App: Android and iOS share these screens and read the same REST API as the Web Portal. */
(function () {
  var ui = { tab: 'home', cat: '', open: null, sos: false };
  var HERE = 'Badulla';

  function row(l) {
    var c = Store.cat(l.cat);
    return '<button type="button" class="mrow" data-open="' + l.id + '"><span class="ic">' + c.icon + '</span><span style="min-width:0"><b>' + esc(l.name) + '</b><small>' + esc(c.name) + ' · ' + esc(l.town) + (l.rating ? ' · ★ ' + l.rating.toFixed(1) : '') + '</small></span></button>';
  }
  function alertCard(a) {
    return '<div class="acard ' + (a.level === 'red' ? '' : a.level) + '"><span class="k">' + esc(a.type) + ' · ' + esc(a.area) + '</span><b>' + esc(a.title) + '</b><small class="mute">' + esc(a.source) + ' · ' + esc(a.time) + '</small></div>';
  }

  function screen(s) {
    var pub = Store.publicListings(), live = s.alerts.filter(function (a) { return a.active; });
    if (ui.tab === 'home') {
      var near = pub.filter(function (l) { return l.district === HERE; });
      return '<div><small class="mute">📍 Ella, Badulla</small><h3>Good morning</h3></div>' + (live[0] ? alertCard(live[0]) : '<div class="acard blue"><b>No active warnings near you</b></div>') +
        '<div class="hs">' + s.categories.filter(function (c) { return c.on && c.kind === 'help'; }).slice(0, 6).map(function (c) { return '<button type="button" class="chip" data-act="mCat" data-cat="' + c.id + '">' + c.icon + ' ' + esc(c.name) + '</button>'; }).join('') + '</div>' +
        '<b>Near you</b>' + near.slice(0, 5).map(row).join('') +
        '<button type="button" class="mrow" data-act="mDonate"><span class="ic">❤️</span><span><b>Donate to disaster response</b><small>Goes to the Red Cross account</small></span></button>';
    }
    if (ui.tab === 'explore') {
      var list = pub.filter(function (l) { return !ui.cat || l.cat === ui.cat; });
      return '<h3>Explore</h3><div class="hs"><button type="button" class="chip' + (ui.cat ? '' : ' on') + '" data-act="mCat" data-cat="">All</button>' +
        s.categories.filter(function (c) { return c.on; }).map(function (c) { return '<button type="button" class="chip' + (ui.cat === c.id ? ' on' : '') + '" data-act="mCat" data-cat="' + c.id + '">' + c.icon + ' ' + esc(c.name) + '</button>'; }).join('') + '</div>' +
        mapHtml(list, { small: true }) + (list.length ? list.map(row).join('') : '<p class="mute">No places in this category yet.</p>');
    }
    if (ui.tab === 'sos') {
      return '<h3>Emergency help</h3><p class="mute small">Hold the button to share your location with your emergency contacts.</p>' +
        '<button type="button" class="sosbtn" data-act="mSos">SOS</button>' +
        (ui.sos ? '<div class="acard"><b>Location shared with 2 contacts</b><small class="mute">Model only. Nothing was sent.</small></div>' : '') +
        s.emergency.map(function (e) { return '<div class="mrow"><span class="ic">📞</span><span><b>' + esc(e.label) + '</b><small class="num">' + esc(e.number) + '</small></span></div>'; }).join('');
    }
    if (ui.tab === 'alerts') {
      return '<h3>Alerts</h3>' + (live.length ? live.map(alertCard).join('') : '<p class="mute">No active alerts.</p>');
    }
    return '<h3>Me</h3>' + [['🌐', 'Language', 'English'], ['👨‍👩‍👧', 'Emergency contacts', '2 contacts'], ['📥', 'Offline content', 'First aid and numbers saved on this phone'], ['🔑', 'Account', 'Same sign-in as the web portal']].map(function (r) {
      return '<div class="mrow"><span class="ic">' + r[0] + '</span><span><b>' + r[1] + '</b><small>' + r[2] + '</small></span></div>';
    }).join('') + '<button type="button" class="mrow" data-act="mDonate"><span class="ic">❤️</span><span><b>Donate</b><small>Support the Red Cross response</small></span></button>';
  }

  Apps.mobile = {
    render: function () {
      var s = Store.get();
      var tabs = [['home', '🏠', 'Home'], ['explore', '🧭', 'Explore'], ['sos', 'SOS', 'Help'], ['alerts', '🔔', 'Alerts'], ['me', '👤', 'Me']];
      var open = ui.open && s.listings.filter(function (l) { return l.id === ui.open; })[0];
      var sheet = open ? '<button type="button" class="sheet-bg" data-act="mClose" aria-label="Close"></button><div class="sheet">' + listingDetail(open) +
        '<div class="row"><button type="button" class="btn btn-red btn-sm" data-act="mCall">Call</button><button type="button" class="btn btn-sm" data-act="mDir">Directions</button><span class="sp"></span><button type="button" class="btn btn-sm" data-act="mClose">Close</button></div></div>' : '';
      return '<div class="wrap stage"><div><span class="eyebrow">Mobile app · Android and iOS</span><h2>The same listings and alerts, in the traveller’s pocket</h2>' +
        '<p class="lead" style="margin-top:10px;max-width:52ch;color:var(--ink2)">The app reads the same REST API as the web portal. A help point added in the Admin Portal appears here straight away, and the safety content stays on the phone when there is no signal.</p>' +
        '<div class="jump">' + tabs.map(function (t) { return '<button type="button" class="chip' + (ui.tab === t[0] ? ' on' : '') + '" data-act="mTab" data-tab="' + t[0] + '">' + t[2] + '</button>'; }).join('') + '</div></div>' +
        '<div class="phone"><div class="screen"><div class="sbar"><span>9:41</span><span>●●● 4G</span></div><div class="scr">' + screen(s) + '</div>' +
        '<nav class="tabbar" aria-label="App">' + tabs.map(function (t) { return '<button type="button" class="' + (t[0] === 'sos' ? 'sos' : '') + '" data-act="mTab" data-tab="' + t[0] + '"' + (ui.tab === t[0] ? ' aria-current="page"' : '') + '><span>' + t[1] + '</span>' + t[2] + '</button>'; }).join('') + '</nav>' + sheet + '</div></div></div>';
    }
  };

  Acts.mTab = function (el) { ui.tab = el.dataset.tab; ui.open = null; ui.sos = false; render(); };
  Acts.mCat = function (el) { ui.tab = 'explore'; ui.cat = el.dataset.cat; render(); };
  Acts.mOpen = function (el) { ui.open = Number(el.dataset.open); render(); };
  Acts.mClose = function () { ui.open = null; render(); };
  Acts.mSos = function () { ui.sos = true; render(); };
  Acts.mCall = function () { var sp = Store.supplier((Store.get().listings.filter(function (l) { return l.id === ui.open; })[0] || {}).supplierId); if (sp) { sp.calls += 1; Store.save(); } toast('The phone dialler opens here'); };
  Acts.mDir = function () { var sp = Store.supplier((Store.get().listings.filter(function (l) { return l.id === ui.open; })[0] || {}).supplierId); if (sp) { sp.directions += 1; Store.save(); } toast('Directions open in the maps app'); };
  Acts.mDonate = function () { toast('The donation page opens here, paid through the same gateway'); };
})();
