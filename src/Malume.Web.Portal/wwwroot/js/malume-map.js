window.malumeMap = (function () {
    const registry = new Map();

    function createOrResetMap(elementId, centerLat, centerLng, zoom) {
        if (typeof L === 'undefined') {
            return null;
        }

        const container = document.getElementById(elementId);
        if (!container) {
            return null;
        }

        let entry = registry.get(elementId);
        if (entry) {
            entry.layerGroup.clearLayers();
            entry.map.invalidateSize();
            return entry;
        }

        const map = L.map(container).setView([centerLat, centerLng], zoom || 14);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        const layerGroup = L.layerGroup().addTo(map);
        entry = { map, layerGroup };
        registry.set(elementId, entry);
        return entry;
    }

    function escapeHtml(text) {
        if (text === null || text === undefined) {
            return '';
        }
        return String(text)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function renderGateBoardMap(elementId, payload) {
        if (!payload) {
            return;
        }

        const entry = createOrResetMap(elementId, payload.gateLatitude, payload.gateLongitude, 14);
        if (!entry) {
            return;
        }

        const { map, layerGroup } = entry;
        const gateLatLng = [payload.gateLatitude, payload.gateLongitude];
        const bounds = [gateLatLng];

        const gateRadius = payload.gateGeofenceRadiusMeters || 150;
        L.circle(gateLatLng, {
            radius: 2000,
            color: '#d97706',
            weight: 1.5,
            dashArray: '6 6',
            fillColor: '#fef3c7',
            fillOpacity: 0.08
        }).addTo(layerGroup);

        L.circle(gateLatLng, {
            radius: gateRadius,
            color: '#16a34a',
            weight: 2,
            fillColor: '#bbf7d0',
            fillOpacity: 0.22
        }).addTo(layerGroup);

        L.circleMarker(gateLatLng, {
            radius: 9,
            color: '#1d4ed8',
            fillColor: '#2563eb',
            fillOpacity: 0.95,
            weight: 2
        })
            .bindPopup(`<strong>${escapeHtml(payload.schoolName)} Gate</strong><br/>Pickup Zone: ${gateRadius} m`)
            .addTo(layerGroup);

        const vehicles = Array.isArray(payload.vehicles) ? payload.vehicles : [];
        vehicles.forEach(function (v) {
            if (typeof v.driverLatitude !== 'number' || typeof v.driverLongitude !== 'number') {
                return;
            }

            const pos = [v.driverLatitude, v.driverLongitude];
            bounds.push(pos);

            let color = '#2563eb';
            if (v.gateZoneStatus === 'AtPickupZone') {
                color = '#16a34a';
            } else if (v.gateZoneStatus === 'Approaching') {
                color = '#d97706';
            } else if (v.tripStatus === 'Cancelled') {
                color = '#dc2626';
            }

            const etaText = typeof v.etaMinutes === 'number' ? `${v.etaMinutes} min` : 'N/A';
            const popupHtml = `<strong>${escapeHtml(v.vehicleRegistration)}</strong> (${escapeHtml(v.gateZoneStatus)})<br/>` +
                `${escapeHtml(v.driverFullName)} &bull; ${escapeHtml(v.routeName)}<br/>` +
                `Boarded: ${v.boardedCount}/${v.totalManifestCount} &bull; ETA: ${etaText}`;

            L.circleMarker(pos, {
                radius: 8,
                color: '#0f172a',
                fillColor: color,
                fillOpacity: 0.95,
                weight: 2
            })
                .bindPopup(popupHtml)
                .addTo(layerGroup);
        });

        if (bounds.length > 1) {
            map.fitBounds(bounds, { padding: [36, 36], maxZoom: 16 });
        } else {
            map.setView(gateLatLng, 14);
        }
    }

    function renderSosMap(elementId, incidents) {
        const list = Array.isArray(incidents) ? incidents : [];
        const first = list[0];
        const centerLat = first ? first.latitude : -26.2041;
        const centerLng = first ? first.longitude : 28.0473;

        const entry = createOrResetMap(elementId, centerLat, centerLng, 13);
        if (!entry) {
            return;
        }

        const { map, layerGroup } = entry;
        const bounds = [];

        list.forEach(function (inc) {
            if (typeof inc.latitude !== 'number' || typeof inc.longitude !== 'number') {
                return;
            }
            const pos = [inc.latitude, inc.longitude];
            bounds.push(pos);

            const isResolved = inc.status === 'Resolved';
            const color = isResolved ? '#64748b' : '#dc2626';

            if (!isResolved) {
                L.circle(pos, {
                    radius: 300,
                    color: '#dc2626',
                    weight: 1.5,
                    fillColor: '#fecaca',
                    fillOpacity: 0.25
                }).addTo(layerGroup);
            }

            const popupHtml = `<strong>${escapeHtml(inc.vehicleRegistration)} — ${escapeHtml(inc.status)}</strong><br/>` +
                `Driver: ${escapeHtml(inc.driverName)}<br/>` +
                `Triggered By: ${escapeHtml(inc.triggeredByRole)} &bull; Onboard: ${inc.onboardLearnerCount}<br/>` +
                `${escapeHtml(inc.notes || 'Emergency SOS Alert')}`;

            L.circleMarker(pos, {
                radius: 9,
                color: '#450a0a',
                fillColor: color,
                fillOpacity: 0.95,
                weight: 2
            })
                .bindPopup(popupHtml)
                .addTo(layerGroup);
        });

        if (bounds.length > 1) {
            map.fitBounds(bounds, { padding: [36, 36], maxZoom: 15 });
        } else if (bounds.length === 1) {
            map.setView(bounds[0], 15);
        }
    }

    function destroyMap(elementId) {
        const entry = registry.get(elementId);
        if (entry) {
            entry.map.remove();
            registry.delete(elementId);
        }
    }

    return {
        renderGateBoardMap,
        renderSosMap,
        destroyMap
    };
})();
