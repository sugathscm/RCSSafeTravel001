/* Shell: switches between the four applications and wires clicks, forms and inputs. */
(function () {
  var current = 'portal';
  var names = ['portal', 'mobile', 'admin', 'supplier'];
  var toastTimer;

  window.go = function (app) {
    current = names.indexOf(app) >= 0 ? app : 'portal';
    try { if (location.hash !== '#' + current) history.replaceState(null, '', '#' + current); } catch (e) { /* optional */ }
    window.render();
    window.scrollTo(0, 0);
  };

  window.render = function () {
    document.getElementById('app').innerHTML = window.Apps[current].render();
    var tabs = document.querySelectorAll('.mtabs button');
    for (var i = 0; i < tabs.length; i++) tabs[i].setAttribute('aria-selected', tabs[i].dataset.app === current ? 'true' : 'false');
  };

  window.toast = function (msg) {
    var o = document.getElementById('overlay');
    var old = o.querySelector('.toast'); if (old) old.remove();
    var t = document.createElement('div'); t.className = 'toast'; t.setAttribute('role', 'status'); t.textContent = msg;
    o.appendChild(t);
    clearTimeout(toastTimer); toastTimer = setTimeout(function () { t.remove(); }, 3200);
  };

  window.closeModal = function () { var m = document.querySelector('#overlay .scrim'); if (m) m.remove(); };
  window.modal = function (html) {
    window.closeModal();
    var s = document.createElement('div'); s.className = 'scrim'; s.innerHTML = '<div class="modal" role="dialog" aria-modal="true">' + html + '</div>';
    s.addEventListener('click', function (e) { if (e.target === s) window.closeModal(); });
    document.getElementById('overlay').appendChild(s);
    var b = s.querySelector('button'); if (b) b.focus();
  };

  window.listingDetail = function (l) {
    var c = Store.cat(l.cat);
    var stars = l.rating ? '<span class="pill warn">★ ' + l.rating.toFixed(1) + ' · ' + l.reviews + ' reviews</span>' : '';
    return '<div class="row"><span class="pill">' + c.icon + ' ' + esc(c.name) + '</span>' +
      (l.verified ? '<span class="pill ok">✓ Verified</span>' : '') + stars + '</div>' +
      '<h3>' + esc(l.name) + '</h3>' +
      (l.desc ? '<p>' + esc(l.desc) + '</p>' : '') +
      '<dl class="kv"><dt>Town</dt><dd>' + esc(l.town) + ', ' + esc(l.district) + '</dd><dt>Phone</dt><dd class="num">' + esc(l.phone) + '</dd><dt>Hours</dt><dd>' + esc(l.hours) + '</dd></dl>';
  };

  window.Acts.reset = function () { Store.reset(); window.toast('Sample data reset'); };
  window.Acts.closeModal = function () { window.closeModal(); };

  document.addEventListener('click', function (e) {
    var tab = e.target.closest('.mtabs button'); if (tab) { window.go(tab.dataset.app); return; }
    var op = e.target.closest('[data-open]');
    if (op) {
      var id = Number(op.dataset.open);
      var l = Store.get().listings.filter(function (x) { return x.id === id; })[0];
      if (!l) return;
      if (op.closest('.phone')) { window.Acts.mOpen(op); return; }
      window.modal(window.listingDetail(l) + '<div class="row"><span class="sp"></span><button type="button" class="btn btn-ink" data-act="closeModal">Close</button></div>');
      return;
    }
    var a = e.target.closest('[data-act]');
    if (a && window.Acts[a.dataset.act]) window.Acts[a.dataset.act](a, e);
  });
  document.addEventListener('submit', function (e) {
    var f = e.target.closest('form[data-form]'); if (!f) return;
    e.preventDefault();
    var d = {}; var els = f.elements;
    for (var i = 0; i < els.length; i++) if (els[i].name) d[els[i].name] = els[i].type === 'checkbox' ? els[i].checked : els[i].value.trim();
    if (window.Forms[f.dataset.form]) window.Forms[f.dataset.form](d, f);
  });
  function onInput(e) {
    var el = e.target.closest('[data-on]'); if (!el) return;
    var pick = el.tagName === 'SELECT' || el.type === 'checkbox';
    if ((e.type === 'input') === pick) return;
    if (window.Acts[el.dataset.on]) window.Acts[el.dataset.on](el, e);
  }
  document.addEventListener('input', onInput);
  document.addEventListener('change', onInput);
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') window.closeModal(); });

  Store.onChange(function () { window.render(); });
  window.addEventListener('hashchange', function () { var h = location.hash.replace('#', ''); if (names.indexOf(h) >= 0 && h !== current) window.go(h); });
  window.go(location.hash.replace('#', '') || 'portal');
})();
