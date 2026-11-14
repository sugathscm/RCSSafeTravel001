/* Web Portal: the public pages. Everything is read from the REST API, the same one the mobile apps use. */
(function ($) {
  'use strict';
  var e = ST.esc;

  function card(l) {
    return '<a class="lcard' + (l.promotion ? ' has-promo' : '') + '" href="/listing/' + l.id + '">' + ST.promoBadge(l) + '<span class="ic">' + e(l.icon) + '</span><h3>' + e(l.name) + '</h3>' +
      '<span class="meta">' + e(l.category) + ' · ' + e(l.town) + (l.distanceKm != null ? ' · ' + l.distanceKm + ' km' : '') + '</span>' + ST.promoLine(l) + '<span class="hrow">' +
      (l.isVerified ? '<span class="pill ok">✓ Verified</span>' : '') + (l.rating ? '<span class="pill warn">★ ' + l.rating.toFixed(1) + '</span>' : '') +
      (l.kind === 'help' && l.phone ? '<span class="pill info num">' + e(l.phone) + '</span>' : '') + '</span></a>';
  }

  // ----- listing details, opened from any card or map pin -----
  function openListing(id) {
    $.when(ST.api('GET', 'listings/' + id), ST.api('GET', 'listings/' + id + '/products', null, true)).done(function (a, b) {
      var l = a[0], tel = (l.phone || '').replace(/[^0-9+]/g, '');
      ST.modal(ST.listingDetail(l) + ST.productList(b[0]) + '<div class="hrow"><span class="sp"></span>' +
        (tel ? '<a class="btn btn-danger" href="tel:' + tel + '" data-track="call" data-id="' + l.id + '">Call</a>' : '') +
        '<a class="btn btn-plain" target="_blank" rel="noopener" href="https://www.google.com/maps/dir/?api=1&destination=' + l.latitude + ',' + l.longitude + '" data-track="directions" data-id="' + l.id + '">Directions</a>' +
        '<a class="btn btn-dark" href="/listing/' + l.id + '">Open page</a><button type="button" class="btn btn-plain" data-close>Close</button></div>');
    });
  }
  function track(id, what) { ST.api('POST', 'listings/' + id + '/track/' + what, null, true); }
  $(document).on('click', '[data-open]', function () { openListing(this.dataset.open); });
  $(document).on('click', '[data-track]', function () { track(this.dataset.id, this.dataset.track); });

  // ----- sponsor advertisements: count the click, the link opens in a new tab -----
  $(document).on('click', '[data-ad]', function () { ST.api('POST', 'ads/' + this.dataset.ad + '/click', null, true); });

  // ----- a listing's own page: count the view and show where it is -----
  var $lp = $('#listing-page');
  if ($lp.length) {
    track($lp.data('id'), 'view');
    if ($('#listing-map').length && window.L) {
      var at = [Number($lp.data('lat')), Number($lp.data('lng'))];
      var lmap = L.map('listing-map', { scrollWheelZoom: false }).setView(at, 14);
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18, attribution: '&copy; OpenStreetMap contributors' }).addTo(lmap);
      L.marker(at, { icon: L.divIcon({ className: 'pin', html: e($lp.data('icon')), iconSize: [30, 30] }) }).addTo(lmap);
    }
  }

  // ----- home: map of help points -----
  var $map = $('#help-map');
  if ($map.length && window.L) {
    var map = L.map($map[0], { scrollWheelZoom: false }).setView([7.6, 80.7], 7);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18, attribution: '&copy; OpenStreetMap contributors' }).addTo(map);
    ST.api('GET', 'listings', { kind: 'help', pageSize: 200 }, true).done(function (r) {
      r.items.forEach(function (l) {
        if (!l.latitude && !l.longitude) return;
        L.marker([l.latitude, l.longitude], { title: l.name, icon: L.divIcon({ className: 'pin', html: e(l.icon), iconSize: [30, 30] }) })
          .addTo(map).on('click', function () { openListing(l.id); });
      });
    });
  }

  // ----- safety map: risk zones, alerts and help points as layers that can be switched on and off -----
  var $sm = $('#safety-map');
  if ($sm.length && window.L) {
    var ZONES = { flood: ['🌊', 'Flood zone', '#2f6fd6'], landslide: ['⛰️', 'Landslide zone', '#c3461d'], roadclosure: ['🚧', 'Road closure', '#a87708'], beachflag: ['🚩', 'Beach flag', '#c62828'], wildlife: ['🐘', 'Wildlife', '#24703c'], other: ['⚠️', 'Risk', '#6b665e'] };
    var qs0 = new URLSearchParams(location.search), flat = Number(qs0.get('lat')), flng = Number(qs0.get('lng'));
    var focus = qs0.get('lat') && qs0.get('lng') && isFinite(flat) && isFinite(flng);
    var smap = L.map($sm[0]).setView(focus ? [flat, flng] : [7.6, 80.7], focus ? 12 : 8), groups = {}, counts = {}, latest = null;
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18, attribution: '&copy; OpenStreetMap contributors' }).addTo(smap);

    function group(key) { return groups[key] || (groups[key] = L.layerGroup()); }
    function count(key) { counts[key] = (counts[key] || 0) + 1; }
    function seen(iso) { if (iso && (!latest || iso > latest)) latest = iso; }
    function pin(key, lat, lng, icon, cls, title, sel) {
      L.marker([lat, lng], { title: title, icon: L.divIcon({ className: 'pin ' + cls, html: e(icon), iconSize: [34, 34] }) }).addTo(group(key)).on('click', function () { select(sel); });
    }
    function select(s) {
      var tel = (s.phone || '').replace(/[^0-9+]/g, '');
      $('#map-sel').removeClass('d-none').html('<div class="head"><span class="ic">' + e(s.icon) + '</span><span class="names"><span class="kk">' + e(s.kicker) + '</span><b class="nm">' + e(s.name) + '</b></span>' +
        '<button type="button" class="x" data-mapsel-close aria-label="Close">✕</button></div>' + (s.details ? '<p>' + e(s.details) + '</p>' : '') + '<div class="hrow">' +
        '<a class="btn btn-dark" target="_blank" rel="noopener" href="https://www.google.com/maps/dir/?api=1&destination=' + s.lat + ',' + s.lng + '"' + (s.id ? ' data-track="directions" data-id="' + s.id + '"' : '') + '>Directions</a>' +
        (tel ? '<a class="btn btn-plain" href="tel:' + tel + '" data-track="call" data-id="' + s.id + '">Call ' + e(s.phone) + '</a>' : '') +
        (s.id ? '<a class="btn btn-plain" href="/listing/' + s.id + '">Open page</a>' : '') + '</div>');
    }
    function paint() {
      $('.layer').each(function () {
        var key = this.dataset.layer, on = $(this).hasClass('on'), g = groups[key];
        $(this).attr('aria-pressed', on).find('.n').text(counts[key] || 0);
        if (g) { if (on) g.addTo(smap); else smap.removeLayer(g); }
      });
      $('#map-updated').text(latest ? ' · updated ' + ST.ago(latest).toLowerCase() : '');
    }
    $(document).on('click', '.layer', function () { $(this).toggleClass('on'); paint(); });
    $(document).on('click', '[data-mapsel-close]', function () { $('#map-sel').addClass('d-none').empty(); });

    ST.api('GET', 'risk-zones', null, true).done(function (zones) {
      zones.forEach(function (z) {
        var t = ZONES[z.type] || ZONES.other, key = 'zone-' + (ZONES[z.type] ? z.type : 'other');
        var sel = { icon: t[0], kicker: t[1] + (z.source ? ' · ' + z.source : ''), name: z.name, details: z.details, lat: z.latitude, lng: z.longitude };
        if (z.radiusMeters > 0) L.circle([z.latitude, z.longitude], { radius: z.radiusMeters, color: t[2], weight: 2, fillColor: t[2], fillOpacity: 0.16 }).addTo(group(key)).on('click', function () { select(sel); });
        pin(key, z.latitude, z.longitude, t[0], 'risk', z.name, sel);
        count(key); seen(z.updatedUtc);
      });
      paint();
    });
    ST.api('GET', 'alerts', null, true).done(function (alerts) {
      alerts.forEach(function (a) {
        seen(a.publishedUtc);
        if (a.latitude == null || a.longitude == null) return;
        pin('alerts', a.latitude, a.longitude, '🔔', 'alert', a.title, { icon: '🔔', kicker: a.type + ' · ' + a.area, name: a.title, details: a.details, lat: a.latitude, lng: a.longitude });
        count('alerts');
      });
      paint();
    });
    ST.api('GET', 'listings', { kind: 'help', pageSize: 200 }, true).done(function (r) {
      r.items.forEach(function (l) {
        if (!l.latitude && !l.longitude) return;
        var key = 'help-' + l.categorySlug;
        pin(key, l.latitude, l.longitude, l.icon, '', l.name, { icon: l.icon, kicker: l.category, name: l.name, details: [l.town, l.hours].filter(Boolean).join(' · '), lat: l.latitude, lng: l.longitude, phone: l.phone, id: l.id });
        count(key);
      });
      paint();
    });
    paint();
  }

  // ----- find help and places -----
  var $find = $('#find');
  if ($find.length) {
    var qs = new URLSearchParams(location.search);
    var f = { kind: qs.get('kind') || '', category: qs.get('category') || '', district: '', q: '', promo: qs.get('promo') === 'true', page: 1, lat: null, lng: null }, timer, total = 0;

    ST.api('GET', 'districts', null, true).done(function (ds) {
      $('#f-district').append(ds.map(function (d) { return '<option>' + e(d) + '</option>'; }).join(''));
    });

    function paint() {
      $find.find('.chip[data-kind]').each(function () { $(this).toggleClass('on', this.dataset.kind === f.kind).attr('aria-pressed', this.dataset.kind === f.kind); });
      $('#f-promo').toggleClass('on', f.promo).attr('aria-pressed', f.promo);
      $('#f-cat option').each(function () { if (this.value) this.hidden = this.disabled = !!f.kind && this.dataset.kind !== f.kind; });
      $('#f-cat').val(f.category);
      $('#f-near').toggleClass('btn-dark', f.lat != null).toggleClass('btn-plain', f.lat == null);
    }
    function load(more) {
      if (!more) f.page = 1;
      var q = { page: f.page, pageSize: 24 };
      ['kind', 'category', 'district', 'q'].forEach(function (k) { if (f[k]) q[k] = f[k]; });
      if (f.promo) q.promo = true;
      if (f.lat != null) { q.lat = f.lat; q.lng = f.lng; q.radiusKm = 50; }
      ST.api('GET', 'listings', q).done(function (r) {
        total = r.total;
        $('#f-count').text(r.total + (r.total === 1 ? ' place found' : ' places found') + (f.lat != null ? ' within 50 km of you' : ''));
        var html = r.items.map(card).join('');
        if (more) $('#f-results').append(html); else $('#f-results').html(html || '<div class="empty">Nothing matches. Clear a filter to see more places.</div>');
        $('#f-more').toggleClass('d-none', f.page * 24 >= total);
      });
    }
    $('#f-promo').on('click', function () { f.promo = !f.promo; paint(); load(); });
    $find.on('click', '.chip[data-kind]', function () { f.kind = this.dataset.kind; f.category = ''; paint(); load(); });
    $('#f-cat').on('change', function () { f.category = this.value; load(); });
    $('#f-district').on('change', function () { f.district = this.value; load(); });
    $('#f-q').on('input', function () { var v = this.value; clearTimeout(timer); timer = setTimeout(function () { f.q = $.trim(v); load(); }, 250); });
    $('#f-more').on('click', function () { f.page++; load(true); });
    $('#f-near').on('click', function () {
      if (f.lat != null) { f.lat = f.lng = null; paint(); load(); return; }
      if (!navigator.geolocation) { ST.toast('This browser cannot share your location.', true); return; }
      navigator.geolocation.getCurrentPosition(function (p) { f.lat = p.coords.latitude; f.lng = p.coords.longitude; paint(); load(); },
        function () { ST.toast('Location is off. Allow it in your browser to see places near you.', true); }, { timeout: 8000 });
    });
    paint(); load();
  }

  // ----- donate -----
  var $don = $('#donate-form');
  if ($don.length) {
    $don.on('click', '[data-amount]', function () { $('#d-amount').val(this.dataset.amount).trigger('input'); });
    $('#d-amount').on('input', function () { var v = this.value; $don.find('[data-amount]').each(function () { $(this).toggleClass('on', this.dataset.amount === v); }); });
    $don.on('submit', function (ev) {
      ev.preventDefault();
      var d = ST.form(this);
      if (!d.amount || d.amount < 100) { ST.toast('Enter an amount of LKR 100 or more.', true); return; }
      ST.busy(this, ST.api('POST', 'donations', d)).done(function (p) {
        if (p.status === 'Paid') { ST.modal('<h3>Thank you</h3><p>' + ST.lkr(p.amount) + ' received. Your reference is <b class="num">' + e(p.reference || p.id) + '</b>.</p><div class="hrow"><span class="sp"></span><button type="button" class="btn btn-dark" data-close>Close</button></div>'); $don[0].reset(); $('#d-amount').trigger('input'); }
        else ST.toast('The payment did not go through. No money was taken. Try again.', true);
      });
    });
  }

  // ----- register a business -----
  $('#register-form').on('submit', function (ev) {
    ev.preventDefault();
    if (!this.checkValidity()) { this.reportValidity(); return; }
    var d = ST.form(this); d.categoryId = Number(d.categoryId);
    ST.busy(this, ST.api('POST', 'suppliers/register', d)).done(function () {
      ST.modal('<h3>Registration sent</h3><p>The admin team will check your details. Sign in with your email and password to upload your documents and follow the review.</p>' +
        '<div class="hrow"><span class="sp"></span><a class="btn btn-danger" href="/account/login?returnUrl=%2Fsupplier">Sign in</a></div>');
    });
  });
})(jQuery);
