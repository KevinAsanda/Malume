export interface LatLngPoint {
  latitude: number;
  longitude: number;
}

export type ProjectedPoint<T extends LatLngPoint> = T & {
  xPct: number;
  yPct: number;
};

const EARTH_RADIUS_METERS = 6_371_000;
const PADDING_RATIO = 0.12;
const INNER_SPAN = 1 - PADDING_RATIO * 2;

export function haversineDistanceMeters(a: LatLngPoint, b: LatLngPoint): number {
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const dLat = toRad(b.latitude - a.latitude);
  const dLng = toRad(b.longitude - a.longitude);
  const lat1 = toRad(a.latitude);
  const lat2 = toRad(b.latitude);

  const sinDLat = Math.sin(dLat / 2);
  const sinDLng = Math.sin(dLng / 2);
  const h = sinDLat * sinDLat + Math.cos(lat1) * Math.cos(lat2) * sinDLng * sinDLng;
  return 2 * EARTH_RADIUS_METERS * Math.asin(Math.min(1, Math.sqrt(h)));
}

export function projectMapPoints<T extends LatLngPoint>(points: T[]): Array<ProjectedPoint<T>> {
  if (points.length === 0) {
    return [];
  }

  let minLat = points[0].latitude;
  let maxLat = points[0].latitude;
  let minLng = points[0].longitude;
  let maxLng = points[0].longitude;

  for (const pt of points) {
    if (pt.latitude < minLat) {
      minLat = pt.latitude;
    }
    if (pt.latitude > maxLat) {
      maxLat = pt.latitude;
    }
    if (pt.longitude < minLng) {
      minLng = pt.longitude;
    }
    if (pt.longitude > maxLng) {
      maxLng = pt.longitude;
    }
  }

  const latSpan = maxLat - minLat;
  const lngSpan = maxLng - minLng;

  return points.map(pt => {
    const xPct =
      lngSpan < 1e-6 ? 0.5 : PADDING_RATIO + ((pt.longitude - minLng) / lngSpan) * INNER_SPAN;
    const yPct =
      latSpan < 1e-6 ? 0.5 : PADDING_RATIO + ((maxLat - pt.latitude) / latSpan) * INNER_SPAN;
    return {
      ...pt,
      xPct: Number(xPct.toFixed(4)),
      yPct: Number(yPct.toFixed(4)),
    };
  });
}

export function buildExternalMapUrl(lat: number, lng: number, _label?: string): string {
  const fLat = lat.toFixed(6);
  const fLng = lng.toFixed(6);
  return `https://www.openstreetmap.org/?mlat=${fLat}&mlon=${fLng}#map=16/${fLat}/${fLng}`;
}
