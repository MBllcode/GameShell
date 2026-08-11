export type GeometryRect = { left: number; top: number; width: number; height: number };
export type GeometryPoint = { x: number; y: number };
export type GeometryBounds = { minX: number; minY: number; maxX: number; maxY: number; width: number; height: number };
export type CanvasTransform = { scale: number; offsetX: number; offsetY: number; padding: number };

const DEFAULT_SNAP_THRESHOLD = 28;
const DEFAULT_ALIGNMENT_THRESHOLD = 24;
const EPSILON = 0.01;

export function rectFromPosition(positionX: number, positionY: number, width: number, height: number): GeometryRect {
  return { left: positionX, top: positionY, width: Math.max(width, 1), height: Math.max(height, 1) };
}

export function rectRight(rect: GeometryRect) { return rect.left + rect.width; }
export function rectBottom(rect: GeometryRect) { return rect.top + rect.height; }

export function rectsOverlap(left: GeometryRect, right: GeometryRect) {
  return left.left < rectRight(right) - EPSILON
    && rectRight(left) > right.left + EPSILON
    && left.top < rectBottom(right) - EPSILON
    && rectBottom(left) > right.top + EPSILON;
}

export function computeTopologyBounds(rects: GeometryRect[]): GeometryBounds {
  if (rects.length === 0) return { minX: 0, minY: 0, maxX: 1, maxY: 1, width: 1, height: 1 };
  const minX = Math.min(...rects.map(rect => rect.left));
  const minY = Math.min(...rects.map(rect => rect.top));
  const maxX = Math.max(...rects.map(rect => rectRight(rect)));
  const maxY = Math.max(...rects.map(rect => rectBottom(rect)));
  return { minX, minY, maxX, maxY, width: Math.max(maxX - minX, 1), height: Math.max(maxY - minY, 1) };
}

export function createCanvasTransform(bounds: GeometryBounds, viewportWidth: number, viewportHeight: number, padding = 24, largestDisplayDimension = 0, maxVisualDisplaySize = 440): CanvasTransform {
  const availableWidth = Math.max(viewportWidth - padding * 2, 1);
  const availableHeight = Math.max(viewportHeight - padding * 2, 1);
  const fitScale = Math.min(availableWidth / bounds.width, availableHeight / bounds.height);
  const sizeScale = largestDisplayDimension > 0 ? maxVisualDisplaySize / largestDisplayDimension : fitScale;
  const scale = Math.min(fitScale, sizeScale);
  return {
    scale,
    offsetX: padding - bounds.minX * scale + (availableWidth - bounds.width * scale) / 2,
    offsetY: padding - bounds.minY * scale + (availableHeight - bounds.height * scale) / 2,
    padding
  };
}

export function topologyToCanvas(point: GeometryPoint, transform: CanvasTransform): GeometryPoint {
  return { x: point.x * transform.scale + transform.offsetX, y: point.y * transform.scale + transform.offsetY };
}

export function canvasToTopology(point: GeometryPoint, transform: CanvasTransform): GeometryPoint {
  return { x: (point.x - transform.offsetX) / transform.scale, y: (point.y - transform.offsetY) / transform.scale };
}

export function rectToCanvas(rect: GeometryRect, transform: CanvasTransform): GeometryRect {
  const point = topologyToCanvas({ x: rect.left, y: rect.top }, transform);
  return { left: point.x, top: point.y, width: rect.width * transform.scale, height: rect.height * transform.scale };
}

function alignmentCandidates(desired: GeometryRect, other: GeometryRect, threshold: number) {
  const candidates: GeometryRect[] = [];
  const verticalAlignments = [other.top, other.top + (other.height - desired.height) / 2, rectBottom(other) - desired.height]
    .filter(top => Math.abs(top - desired.top) <= threshold);
  const horizontalAlignments = [other.left, other.left + (other.width - desired.width) / 2, rectRight(other) - desired.width]
    .filter(left => Math.abs(left - desired.left) <= threshold);

  if (Math.abs(desired.left - rectRight(other)) <= threshold) verticalAlignments.forEach(top => candidates.push({ ...desired, left: rectRight(other), top }));
  if (Math.abs(rectRight(desired) - other.left) <= threshold) verticalAlignments.forEach(top => candidates.push({ ...desired, left: other.left - desired.width, top }));
  if (Math.abs(desired.top - rectBottom(other)) <= threshold) horizontalAlignments.forEach(left => candidates.push({ ...desired, top: rectBottom(other), left }));
  if (Math.abs(rectBottom(desired) - other.top) <= threshold) horizontalAlignments.forEach(left => candidates.push({ ...desired, top: other.top - desired.height, left }));
  return candidates;
}

export function applyEdgeSnap(desired: GeometryRect, others: GeometryRect[], threshold = DEFAULT_SNAP_THRESHOLD, alignmentThreshold = DEFAULT_ALIGNMENT_THRESHOLD) {
  const candidates = others.flatMap(other => alignmentCandidates(desired, other, threshold));
  const candidate = candidates
    .filter(value => Math.abs(value.left - desired.left) <= threshold || Math.abs(value.top - desired.top) <= threshold)
    .sort((left, right) => Math.hypot(left.left - desired.left, left.top - desired.top) - Math.hypot(right.left - desired.left, right.top - desired.top))[0];
  if (!candidate) return desired;

  const aligned = others.flatMap(other => alignmentCandidates(candidate, other, alignmentThreshold));
  return aligned.sort((left, right) => Math.hypot(left.left - candidate.left, left.top - candidate.top) - Math.hypot(right.left - candidate.left, right.top - candidate.top))[0] ?? candidate;
}

function collisionCandidates(desired: GeometryRect, other: GeometryRect) {
  return [
    { ...desired, left: other.left - desired.width },
    { ...desired, left: rectRight(other) },
    { ...desired, top: other.top - desired.height },
    { ...desired, top: rectBottom(other) }
  ];
}

export function resolveCollision(desired: GeometryRect, others: GeometryRect[]) {
  let resolved = desired;
  for (let attempt = 0; attempt < others.length + 1; attempt += 1) {
    const collision = others.find(other => rectsOverlap(resolved, other));
    if (!collision) return resolved;
    const candidates = collisionCandidates(resolved, collision)
      .filter(candidate => !others.some(other => other !== collision && rectsOverlap(candidate, other)))
      .sort((left, right) => Math.hypot(left.left - resolved.left, left.top - resolved.top) - Math.hypot(right.left - resolved.left, right.top - resolved.top));
    if (!candidates[0]) return resolved;
    resolved = candidates[0];
  }
  return resolved;
}

export function snapAndResolve(desired: GeometryRect, others: GeometryRect[]) {
  return resolveCollision(applyEdgeSnap(desired, others), others);
}
