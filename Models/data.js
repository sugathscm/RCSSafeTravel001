/* Safe Travel Sri Lanka model: shared sample data and store.
   In the real build this is the MSSQL database behind the REST API. */
(function () {
  var KEY = 'stsl-model-v1';

  var seed = {
    settings: {
      heroTitle: "Explore Sri Lanka. We've got your back.",
      heroText: 'Live flood, landslide and storm alerts, a safety map for the whole island, first aid and one-tap SOS. Free for every traveller.',
      annualFee: 1000,
      sections: { alerts: true, map: true, businesses: true, topics: true, donate: true, sponsors: true }
    },
    emergency: [
      { label: 'Police', number: '119' },
      { label: 'Ambulance', number: '1990' },
      { label: 'Tourist hotline', number: '1912' },
      { label: 'Disaster', number: '117' },
      { label: 'Fire & rescue', number: '110' }
    ],
    categories: [
      { id: 'bank', kind: 'help', name: 'Banks & ATMs', icon: '🏦', on: true },
      { id: 'sos', kind: 'help', name: 'SOS points', icon: '🆘', on: true },
      { id: 'police', kind: 'help', name: 'Police stations', icon: '👮', on: true },
      { id: 'hospital', kind: 'help', name: 'Hospitals', icon: '🏥', on: true },
      { id: 'pharmacy', kind: 'help', name: 'Pharmacies', icon: '💊', on: true },
      { id: 'embassy', kind: 'help', name: 'Embassies', icon: '🏛️', on: true },
      { id: 'exchange', kind: 'help', name: 'Money exchange', icon: '💱', on: true },
      { id: 'fuel', kind: 'help', name: 'Fuel stations', icon: '⛽', on: true },
      { id: 'transport', kind: 'help', name: 'Stations & airports', icon: '🚉', on: true },
      { id: 'shelter', kind: 'help', name: 'Shelters', icon: '🏠', on: true },
      { id: 'stay', kind: 'business', name: 'Hotels & stays', icon: '🛏️', on: true },
      { id: 'food', kind: 'business', name: 'Restaurants', icon: '🍛', on: true },
      { id: 'tour', kind: 'business', name: 'Tours & guides', icon: '🧭', on: true },
      { id: 'ride', kind: 'business', name: 'Drivers & transport', icon: '🚕', on: true },
      { id: 'rental', kind: 'business', name: 'Vehicle rental', icon: '🛵', on: true },
      { id: 'activity', kind: 'business', name: 'Activities', icon: '🏄', on: true },
      { id: 'wellness', kind: 'business', name: 'Wellness & Ayurveda', icon: '🌿', on: true }
    ],
    listings: [
      { id: 1, cat: 'bank', name: 'Lakeside Bank, Fort branch', town: 'Colombo', district: 'Colombo', phone: '011 555 0101', hours: 'Mon to Fri 9:00 to 15:00, ATM 24 hours', lat: 6.934, lng: 79.845, status: 'published', desc: 'Foreign card ATM and currency counter.' },
      { id: 2, cat: 'bank', name: 'Hill Country Bank ATM', town: 'Ella', district: 'Badulla', phone: '057 555 0102', hours: 'ATM 24 hours', lat: 6.867, lng: 81.046, status: 'published', desc: 'Accepts Visa and Mastercard.' },
      { id: 3, cat: 'bank', name: 'Southern Bank, Galle Fort', town: 'Galle', district: 'Galle', phone: '091 555 0103', hours: 'Mon to Fri 9:00 to 15:00', lat: 6.027, lng: 80.217, status: 'published', desc: 'Branch with two ATMs inside the fort.' },
      { id: 4, cat: 'sos', name: 'Ella town SOS point', town: 'Ella', district: 'Badulla', phone: '119', hours: '24 hours', lat: 6.875, lng: 81.049, status: 'published', desc: 'Staffed help point beside the railway station.' },
      { id: 5, cat: 'sos', name: 'Mirissa beach lifeguard post', town: 'Mirissa', district: 'Matara', phone: '041 555 0105', hours: 'Daily 7:00 to 18:00', lat: 5.946, lng: 80.458, status: 'published', desc: 'Lifeguards and beach flag updates.' },
      { id: 6, cat: 'police', name: 'Tourist Police, Kandy', town: 'Kandy', district: 'Kandy', phone: '081 555 0106', hours: '24 hours', lat: 7.293, lng: 80.635, status: 'published', desc: 'English-speaking officers.' },
      { id: 7, cat: 'police', name: 'Ella Police Station', town: 'Ella', district: 'Badulla', phone: '057 555 0107', hours: '24 hours', lat: 6.871, lng: 81.047, status: 'published', desc: '' },
      { id: 8, cat: 'police', name: 'Galle Fort Police Post', town: 'Galle', district: 'Galle', phone: '091 555 0108', hours: '24 hours', lat: 6.030, lng: 80.215, status: 'published', desc: '' },
      { id: 9, cat: 'hospital', name: 'Badulla General Hospital', town: 'Badulla', district: 'Badulla', phone: '055 555 0109', hours: 'Emergency unit 24 hours', lat: 6.990, lng: 81.055, status: 'published', desc: 'Nearest emergency unit to Ella.' },
      { id: 10, cat: 'hospital', name: 'Kandy National Hospital', town: 'Kandy', district: 'Kandy', phone: '081 555 0110', hours: 'Emergency unit 24 hours', lat: 7.286, lng: 80.631, status: 'published', desc: '' },
      { id: 11, cat: 'pharmacy', name: 'Fort Pharmacy', town: 'Galle', district: 'Galle', phone: '091 555 0111', hours: 'Daily 8:00 to 21:00', lat: 6.028, lng: 80.218, status: 'published', desc: '' },
      { id: 12, cat: 'exchange', name: 'Arrivals currency counter', town: 'Katunayake', district: 'Gampaha', phone: '011 555 0112', hours: '24 hours', lat: 7.180, lng: 79.884, status: 'published', desc: 'Inside the airport arrivals hall.' },
      { id: 13, cat: 'transport', name: 'Ella railway station', town: 'Ella', district: 'Badulla', phone: '057 555 0113', hours: 'Ticket office 6:00 to 18:00', lat: 6.876, lng: 81.047, status: 'published', desc: '' },
      { id: 14, cat: 'shelter', name: 'Ella Central College shelter', town: 'Ella', district: 'Badulla', phone: '057 555 0114', hours: 'Opens when a warning is issued', lat: 6.869, lng: 81.052, status: 'published', desc: 'Capacity 250 people.' },
      { id: 15, cat: 'embassy', name: 'Consular services desk (sample)', town: 'Colombo', district: 'Colombo', phone: '011 555 0115', hours: 'Mon to Fri 8:30 to 16:00', lat: 6.905, lng: 79.861, status: 'published', desc: 'Sample entry. Real embassy contacts are added by the admin team.' },
      { id: 16, cat: 'fuel', name: 'Wellawaya Road fuel station', town: 'Ella', district: 'Badulla', phone: '057 555 0116', hours: 'Daily 6:00 to 22:00', lat: 6.861, lng: 81.044, status: 'published', desc: '' },

      { id: 101, cat: 'stay', name: 'Ella Ridge Homestay', town: 'Ella', district: 'Badulla', phone: '077 555 0201', hours: 'Check-in from 14:00', lat: 6.872, lng: 81.051, status: 'published', supplierId: 1, verified: true, rating: 4.7, reviews: 86, featured: true, desc: 'Family-run homestay with a mountain view, five minutes from the station. Staff trained in first aid.' },
      { id: 102, cat: 'tour', name: 'Nine Arch Walks', town: 'Ella', district: 'Badulla', phone: '077 555 0202', hours: 'Daily 6:00 to 18:00', lat: 6.878, lng: 81.060, status: 'published', supplierId: 2, verified: true, rating: 4.9, reviews: 132, featured: true, desc: 'Licensed guides for Nine Arch Bridge, Little Adam’s Peak and Ella Rock. Routes change when a landslide watch is active.' },
      { id: 103, cat: 'ride', name: 'Lanka Safe Rides', town: 'Kandy', district: 'Kandy', phone: '077 555 0203', hours: '24 hours', lat: 7.291, lng: 80.640, status: 'published', supplierId: 3, verified: true, rating: 4.5, reviews: 54, featured: false, desc: 'Registered drivers for airport and hill-country transfers.' },
      { id: 104, cat: 'food', name: 'Mirissa Catch Kitchen', town: 'Mirissa', district: 'Matara', phone: '077 555 0204', hours: 'Daily 11:00 to 22:00', lat: 5.948, lng: 80.455, status: 'published', supplierId: 4, verified: true, rating: 4.6, reviews: 211, featured: true, desc: 'Seafood on the beach road. Food safety certificate on file.' },
      { id: 105, cat: 'rental', name: 'Coast Scooters', town: 'Galle', district: 'Galle', phone: '077 555 0205', hours: 'Daily 8:00 to 19:00', lat: 6.035, lng: 80.220, status: 'lapsed', supplierId: 5, verified: true, rating: 4.1, reviews: 18, featured: false, desc: 'Scooter and helmet hire with insurance.' },
      { id: 106, cat: 'activity', name: 'Weligama Surf School', town: 'Weligama', district: 'Matara', phone: '077 555 0206', hours: 'Daily 6:30 to 17:30', lat: 5.972, lng: 80.426, status: 'pending', supplierId: 6, verified: false, rating: 0, reviews: 0, featured: false, desc: 'Beginner lessons with qualified lifeguard instructors.' },
      { id: 107, cat: 'wellness', name: 'Sigiri Herbal Spa', town: 'Sigiriya', district: 'Matale', phone: '077 555 0207', hours: 'Daily 9:00 to 20:00', lat: 7.955, lng: 80.757, status: 'pending', supplierId: 7, verified: false, rating: 0, reviews: 0, featured: false, desc: 'Traditional Ayurveda treatments.' }
    ],
    suppliers: [
      { id: 1, business: 'Ella Ridge Homestay', contact: 'Nimal Perera', email: 'owner@ellaridge.example', phone: '077 555 0201', status: 'approved', paidUntil: '2027-01-18', docs: [{ name: 'Business registration', status: 'verified' }, { name: 'Tourist board licence', status: 'verified' }], views: 1240, calls: 96, directions: 310 },
      { id: 2, business: 'Nine Arch Walks', contact: 'Kavindi Silva', email: 'hello@ninearch.example', phone: '077 555 0202', status: 'approved', paidUntil: '2027-06-02', docs: [{ name: 'Business registration', status: 'verified' }, { name: 'Guide licence', status: 'verified' }], views: 2210, calls: 174, directions: 420 },
      { id: 3, business: 'Lanka Safe Rides', contact: 'Mohamed Rizwan', email: 'bookings@safeRides.example', phone: '077 555 0203', status: 'approved', paidUntil: '2026-11-26', docs: [{ name: 'Business registration', status: 'verified' }, { name: 'Vehicle insurance', status: 'verified' }], views: 860, calls: 141, directions: 12 },
      { id: 4, business: 'Mirissa Catch Kitchen', contact: 'Tharushi Fernando', email: 'team@mirissacatch.example', phone: '077 555 0204', status: 'approved', paidUntil: '2027-03-30', docs: [{ name: 'Business registration', status: 'verified' }, { name: 'Food safety certificate', status: 'verified' }], views: 3105, calls: 88, directions: 640 },
      { id: 5, business: 'Coast Scooters', contact: 'Ruwan Jayasuriya', email: 'ruwan@coastscooters.example', phone: '077 555 0205', status: 'approved', paidUntil: '2026-10-31', docs: [{ name: 'Business registration', status: 'verified' }], views: 402, calls: 31, directions: 77 },
      { id: 6, business: 'Weligama Surf School', contact: 'Dilan Wickramasinghe', email: 'dilan@weligamasurf.example', phone: '077 555 0206', status: 'pending', paidUntil: '', docs: [{ name: 'Business registration', status: 'uploaded' }, { name: 'Lifeguard certificates', status: 'uploaded' }], views: 0, calls: 0, directions: 0 },
      { id: 7, business: 'Sigiri Herbal Spa', contact: 'Anusha Kumari', email: 'anusha@sigirispa.example', phone: '077 555 0207', status: 'pending', paidUntil: '', docs: [{ name: 'Business registration', status: 'uploaded' }], views: 0, calls: 0, directions: 0 }
    ],
    alerts: [
      { id: 1, level: 'red', type: 'Landslide watch', area: 'Ella, Badulla', title: 'Heavy rain in the hills until Thursday evening', source: 'NBRO', time: '12 min ago', active: true },
      { id: 2, level: 'blue', type: 'Flood warning', area: 'Ratnapura', title: 'Kalu Ganga rising after heavy rain', source: 'Irrigation Department', time: '2 h ago', active: true },
      { id: 3, level: 'red', type: 'Red flag', area: 'Mirissa, Unawatuna', title: 'No swimming in Mirissa because of rip currents', source: 'Lifeguards', time: '3 h ago', active: true },
      { id: 4, level: 'amber', type: 'Road closure', area: 'Haputale', title: 'A16 closed between Haputale and Beragala', source: 'Road Development Authority', time: 'Yesterday', active: false }
    ],
    payments: [
      { id: 5012, date: '2026-11-11', kind: 'Supplier fee', who: 'Nine Arch Walks', amount: 1000, status: 'Paid' },
      { id: 5011, date: '2026-11-10', kind: 'Donation', who: 'Anonymous donor', amount: 5000, status: 'Paid' },
      { id: 5010, date: '2026-11-09', kind: 'Supplier fee', who: 'Mirissa Catch Kitchen', amount: 1000, status: 'Paid' },
      { id: 5009, date: '2026-11-08', kind: 'Donation', who: 'H. Schmidt', amount: 2500, status: 'Paid' },
      { id: 5008, date: '2026-11-06', kind: 'Supplier fee', who: 'Coast Scooters', amount: 1000, status: 'Failed' },
      { id: 5007, date: '2026-11-04', kind: 'Donation', who: 'A. Tanaka', amount: 10000, status: 'Paid' }
    ],
    sponsors: [
      { id: 1, name: 'Uber', role: 'Safe Travel partner', on: true },
      { id: 2, name: 'Dialog', role: 'Marketing partner', on: true }
    ],
    users: [
      { name: 'Super admin', email: 'admin@safetravel.example', role: 'Super admin', twoFactor: true, active: true },
      { name: 'Content reviewer', email: 'review@safetravel.example', role: 'Content reviewer', twoFactor: true, active: true },
      { name: 'Alerts officer', email: 'alerts@safetravel.example', role: 'Alerts officer', twoFactor: true, active: true },
      { name: 'Supplier manager', email: 'suppliers@safetravel.example', role: 'Supplier manager', twoFactor: true, active: true },
      { name: 'Finance officer', email: 'finance@safetravel.example', role: 'Finance officer', twoFactor: false, active: true },
      { name: 'Call centre agent 1', email: 'agent1@safetravel.example', role: 'Call centre agent', twoFactor: true, active: true }
    ],
    topics: [
      { name: 'Floods, landslides & storms', text: 'Monsoon floods, hill-country landslides, cyclones, lightning and heat.', icon: '🌧️', tone: 'blue' },
      { name: 'Beach & water safety', text: 'Rip currents, monsoon seas, rivers and waterfalls.', icon: '🌊', tone: 'teal' },
      { name: 'Health', text: 'Dengue, food and water, heat illness.', icon: '🦟', tone: 'green' },
      { name: 'First aid', text: 'What to do until help arrives.', icon: '🩹', tone: 'rose' },
      { name: 'Roads & transport', text: 'Tuk-tuks, buses, trains, scooters and night driving.', icon: '🛺', tone: 'sand' },
      { name: 'Wildlife', text: 'Elephants, snakes, monkeys, leeches and safaris.', icon: '🐘', tone: 'olive' }
    ],
    audit: [
      { time: '11 Nov, 16:42', who: 'Alerts officer', what: 'Published alert "Heavy rain in the hills until Thursday evening"' },
      { time: '11 Nov, 11:05', who: 'Supplier manager', what: 'Approved supplier "Nine Arch Walks"' },
      { time: '10 Nov, 09:18', who: 'Super admin', what: 'Turned on two-factor sign-in for Supplier manager' }
    ],
    me: 1,
    nextId: 200
  };

  function clone(o) { return JSON.parse(JSON.stringify(o)); }

  var state;
  try { state = JSON.parse(localStorage.getItem(KEY)); } catch (e) { state = null; }
  if (!state || !state.listings) state = clone(seed);

  var subs = [];
  var Store = {
    get: function () { return state; },
    save: function () {
      try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { /* storage is optional */ }
      subs.forEach(function (f) { f(state); });
    },
    reset: function () { state = clone(seed); Store.save(); },
    onChange: function (f) { subs.push(f); },
    id: function () { state.nextId += 1; return state.nextId; },
    log: function (who, what) {
      var d = new Date();
      var m = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][d.getMonth()];
      var t = d.getDate() + ' ' + m + ', ' + ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2);
      state.audit.unshift({ time: t, who: who, what: what });
    },
    cat: function (id) { return state.categories.filter(function (c) { return c.id === id; })[0] || { name: id, icon: '📍', kind: 'help' }; },
    supplier: function (id) { return state.suppliers.filter(function (s) { return s.id === id; })[0]; },
    listingOf: function (sid) { return state.listings.filter(function (l) { return l.supplierId === sid; })[0]; },
    publicListings: function () {
      return state.listings.filter(function (l) { return l.status === 'published' && Store.cat(l.cat).on; });
    }
  };
  window.Store = Store;
  window.Acts = {}; window.Forms = {}; window.Apps = {};

  window.esc = function (s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  };
  window.lkr = function (n) { return 'LKR ' + Number(n).toLocaleString('en-LK'); };

  /* Simple map: island outline and pins from latitude and longitude. */
  var OUTLINE = [[9.82, 80.20], [9.50, 80.60], [9.27, 80.81], [8.57, 81.23], [7.90, 81.55], [7.72, 81.70], [7.00, 81.88], [6.87, 81.83], [6.37, 81.52], [6.12, 81.12], [5.92, 80.59], [6.03, 80.22], [6.40, 80.00], [6.93, 79.85], [7.20, 79.83], [7.58, 79.79], [8.23, 79.75], [8.60, 79.90], [8.98, 79.90], [9.40, 80.05], [9.66, 80.00]];
  function px(lat, lng) { return [(lng - 79.45) / 2.75 * 100, (9.95 - lat) / 4.2 * 100]; }
  window.mapHtml = function (items, opts) {
    opts = opts || {};
    var pts = OUTLINE.map(function (p) { var q = px(p[0], p[1]); return (q[0] * 0.62).toFixed(1) + ',' + q[1].toFixed(1); }).join(' ');
    var pins = items.map(function (l) {
      var q = px(l.lat, l.lng);
      return '<button type="button" class="pin" data-open="' + l.id + '" style="left:' + q[0].toFixed(1) + '%;top:' + q[1].toFixed(1) + '%" title="' + esc(l.name) + '" aria-label="' + esc(l.name) + '">' + Store.cat(l.cat).icon + '</button>';
    }).join('');
    return '<div class="map' + (opts.small ? ' map-s' : '') + '"><div class="map-in"><svg viewBox="0 0 62 100" preserveAspectRatio="none" aria-hidden="true"><polygon points="' + pts + '"/></svg>' + pins + '</div><span class="map-note">Model map · sample pins</span></div>';
  };
})();
