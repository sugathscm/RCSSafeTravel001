/* Shared by every page: calls to the REST API with jQuery AJAX, messages, the pop-up window and small formatters. */
(function ($) {
  'use strict';
  var token = $('meta[name="csrf-token"]').attr('content');

  // Every change sent from the browser carries the anti-forgery token. The API rejects the call without it.
  $.ajaxSetup({ headers: { 'X-CSRF-TOKEN': token } });

  var ST = window.ST = {};

  ST.esc = function (v) {
    return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
  };
  ST.lkr = function (n) { return 'LKR ' + Number(n || 0).toLocaleString('en-LK', { maximumFractionDigits: 0 }); };
  ST.date = function (iso) { if (!iso) return ''; var d = new Date(iso); return isNaN(d) ? '' : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }); };
  ST.dateTime = function (iso) { var d = utc(iso); return isNaN(d) ? '' : d.toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }); };
  ST.ago = function (iso) {
    var m = Math.round((Date.now() - utc(iso)) / 60000);
    if (isNaN(m)) return '';
    if (m < 1) return 'Just now';
    if (m < 60) return m + ' min ago';
    if (m < 1440) return Math.round(m / 60) + ' h ago';
    return Math.round(m / 1440) + ' d ago';
  };
  function utc(iso) { return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso || '') ? iso : iso + 'Z'); }

  /** Reads a server error and turns it into one sentence for the user. */
  ST.errorText = function (xhr) {
    var p = xhr && xhr.responseJSON;
    if (p && p.errors) { var first = Object.keys(p.errors)[0]; if (first) return [].concat(p.errors[first])[0]; }
    if (p && (p.detail || p.title)) return p.detail || p.title;
    if (xhr && xhr.status === 401) return 'Your session has ended. Sign in again.';
    if (xhr && xhr.status === 403) return 'Your role does not allow this.';
    if (xhr && xhr.status === 429) return 'Too many requests. Wait a minute and try again.';
    return 'Could not reach the server. Check your connection and try again.';
  };

  /** One AJAX call to the REST API. Returns a jQuery promise. Shows the error as a message unless quiet is set. */
  ST.api = function (method, url, data, quiet) {
    var o = { method: method, url: '/api/v1/' + url, dataType: 'json' };
    if (data instanceof FormData) { o.data = data; o.processData = false; o.contentType = false; }
    else if (method === 'GET') { o.data = data; }
    else { o.contentType = 'application/json'; o.data = JSON.stringify(data == null ? {} : data); }
    // An empty reply (204) is a success, not a parse error.
    o.dataFilter = function (raw) { return raw === '' ? 'null' : raw; };
    return $.ajax(o).fail(function (xhr) {
      if (xhr.status === 401 && !quiet) { location.href = '/account/login?returnUrl=' + encodeURIComponent(location.pathname + location.hash); return; }
      if (!quiet) ST.toast(ST.errorText(xhr), true);
    });
  };

  ST.toast = function (msg, bad) {
    var $t = $('<div class="toast' + (bad ? ' bad' : '') + '" role="' + (bad ? 'alert' : 'status') + '"><div class="toast-body"></div></div>');
    $t.find('.toast-body').text(msg);
    $('#toasts').empty().append($t);
    new bootstrap.Toast($t[0], { delay: bad ? 6000 : 3200 }).show();
  };

  ST.modal = function (html) {
    $('#app-modal-body').html(html);
    bootstrap.Modal.getOrCreateInstance('#app-modal').show();
  };
  ST.closeModal = function () { var m = bootstrap.Modal.getInstance('#app-modal'); if (m) m.hide(); };
  $(document).on('click', '[data-close]', ST.closeModal);

  /** Form fields as an object, keyed by name. Checkboxes become true or false, empty text becomes null. */
  ST.form = function (form) {
    var d = {};
    $(form).find('[name]').each(function () {
      if (this.type === 'checkbox') d[this.name] = this.checked;
      else if (this.type === 'file') return;
      else { var v = $.trim(this.value); d[this.name] = v === '' ? null : (this.type === 'number' || this.dataset.num != null ? Number(v) : v); }
    });
    return d;
  };

  /** Stops double submits: disables the button until the call ends. */
  ST.busy = function (form, promise) {
    var $b = $(form).find('[type="submit"]').prop('disabled', true);
    return promise.always(function () { $b.prop('disabled', false); });
  };

  /** The red badge shown on a tile when the listing has a running promotion. */
  ST.promoBadge = function (l) { return l.promotion ? '<span class="promo-badge">Promotion</span>' : ''; };
  ST.promoLine = function (l) { return l.promotion ? '<span class="promo-line">' + ST.esc(l.promotion.title) + '</span>' : ''; };

  ST.listingDetail = function (l) {
    var e = ST.esc;
    return (l.promotion ? '<div class="promo-box"><span class="promo-badge static">Promotion</span><b>' + e(l.promotion.title) + '</b>' + (l.promotion.text ? '<span>' + e(l.promotion.text) + '</span>' : '') +
      (l.promotion.until ? '<span class="small">Until ' + ST.date(l.promotion.until) + '</span>' : '') + '</div>' : '') +
      '<div class="hrow"><span class="pill">' + e(l.icon) + ' ' + e(l.category) + '</span>' +
      (l.isVerified ? '<span class="pill ok">✓ Verified</span>' : '') +
      (l.rating ? '<span class="pill warn">★ ' + l.rating.toFixed(1) + ' · ' + l.reviews + ' reviews</span>' : '') +
      (l.distanceKm != null ? '<span class="pill info">' + l.distanceKm + ' km away</span>' : '') + '</div>' +
      '<h3>' + e(l.name) + '</h3>' + (l.description ? '<p>' + e(l.description) + '</p>' : '') +
      '<dl class="kv"><dt>Town</dt><dd>' + e(l.town) + ', ' + e(l.district) + '</dd>' +
      (l.phone ? '<dt>Phone</dt><dd class="num">' + e(l.phone) + '</dd>' : '') +
      (l.hours ? '<dt>Hours</dt><dd>' + e(l.hours) + '</dd>' : '') + '</dl>';
  };

  /** Products and prices of a listing, as shown to travellers. */
  ST.productList = function (products) {
    var e = ST.esc;
    if (!products || !products.length) return '';
    return '<div class="products"><span class="eyebrow mute">Products and prices</span>' + products.map(function (p) {
      return '<div class="product">' + (p.imageUrl ? '<img src="' + e(p.imageUrl) + '" alt="" loading="lazy">' : '<span class="ph" aria-hidden="true">🛍️</span>') +
        '<div class="pbody"><b>' + e(p.name) + '</b>' + (p.description ? '<span class="small mute">' + e(p.description) + '</span>' : '') + '</div>' +
        '<div class="price num">' + (p.price != null ? ST.lkr(p.price) + (p.unit ? '<span class="small mute">' + e(p.unit) + '</span>' : '') : '<span class="small mute">Ask for price</span>') + '</div></div>';
    }).join('') + '</div>';
  };

  $('time[data-ago]').each(function () { $(this).text(ST.ago(this.getAttribute('datetime'))); });

  // The Red Cross logo is one file, wwwroot/img/slrcs-logo.png. If it is missing, show the neutral mark, not a broken picture.
  $('img[data-logo]').each(function () {
    var img = this;
    function missing() { $(img).parent().addClass('no-logo'); }
    if (img.complete) { if (!img.naturalWidth) missing(); } else $(img).one('error', missing);
  });
})(jQuery);
