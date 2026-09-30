const DHAKA = { minLat: 23.65, maxLat: 23.95, minLng: 90.30, maxLng: 90.55 };

function isInsideDhaka(lat, lng) {
  return lat >= DHAKA.minLat && lat <= DHAKA.maxLat &&
         lng >= DHAKA.minLng && lng <= DHAKA.maxLng;
}

function formatCoords(lat, lng) {
  return lat.toFixed(5) + ', ' + lng.toFixed(5);
}

if (typeof document !== 'undefined') {
  document.addEventListener('DOMContentLoaded', () => {
    const map = L.map('location-map').setView([23.8103, 90.4125], 12);

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap'
    }).addTo(map);

    let marker = null;
    const err = document.getElementById('location-error');

    function setLocation(lat, lng, address) {
      if (!isInsideDhaka(lat, lng)) {
        err.textContent =
          'This location is outside the Dhaka service area.';
        return;
      }

      err.textContent = '';

      if (!marker) {
        marker = L.marker([lat, lng], { draggable: true }).addTo(map);

        marker.on('dragend', () => {
          const p = marker.getLatLng();
          setLocation(p.lat, p.lng);
        });
      } else {
        marker.setLatLng([lat, lng]);
      }

      map.setView([lat, lng], 16);

      document.getElementById('Latitude').value = lat;
      document.getElementById('Longitude').value = lng;

      const addr = document.getElementById('AddressText');

      if (address)
        addr.value = address;
      else if (!addr.value)
        reverseGeocode(lat, lng, addr);

      document.dispatchEvent(
        new CustomEvent('location-changed')
      );
    }

    async function reverseGeocode(lat, lng, input) {
      try {
        const r = await fetch(
          `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}`
        );

        const j = await r.json();

        if (j.display_name && !input.value) {
          input.value = j.display_name;

          document.dispatchEvent(
            new CustomEvent('location-changed')
          );
        }
      } catch {
        /* the resident can type the address manually */
      }
    }

    map.on('click', e =>
      setLocation(e.latlng.lat, e.latlng.lng)
    );

    document.getElementById('AddressText')
      .addEventListener('input', () =>
        document.dispatchEvent(
          new CustomEvent('location-changed')
        )
      );

    document.getElementById('use-my-location')
      .addEventListener('click', () => {
        err.textContent = '';

        if (!navigator.geolocation) {
          err.textContent =
            'Location is not supported. Drag the pin or type an address.';
          return;
        }

        navigator.geolocation.getCurrentPosition(
          p => setLocation(
            p.coords.latitude,
            p.coords.longitude
          ),
          () => {
            err.textContent =
              'Location permission denied or unavailable. Click the map, drag the pin, or type an address.';
          },
          {
            enableHighAccuracy: true,
            timeout: 10000
          }
        );
      });

    window.LocationPicker = { setLocation };
  });
}

if (typeof module !== 'undefined')
  module.exports = { isInsideDhaka, formatCoords };