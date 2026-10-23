(() => {
    function ringContains(ring, x, y) {
        let inside = false;
        for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
            const [xi, yi] = ring[i], [xj, yj] = ring[j];
            const cross = (x - xi) * (yj - yi) - (y - yi) * (xj - xi);
            if (Math.abs(cross) < 1e-10 && x >= Math.min(xi,xj) && x <= Math.max(xi,xj) && y >= Math.min(yi,yj) && y <= Math.max(yi,yj)) return true;
            if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }
    function contains(geometry, latitude, longitude) {
        if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return false;
        try {
            const polygon = rings => rings.length > 0 && ringContains(rings[0],longitude,latitude) && !rings.slice(1).some(ring => ringContains(ring,longitude,latitude));
            return geometry?.type === 'Polygon' ? polygon(geometry.coordinates) : geometry?.type === 'MultiPolygon' ? geometry.coordinates.some(polygon) : false;
        } catch { return false; }
    }
    globalThis.ZoneLocator = { contains };
})();
