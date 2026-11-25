using System;
using System.IO;

namespace SearchBrowser
{
    // Exact 1:1 Canvas 2D port of oriel-ref/src/ui/themes/ (random.ts, draw.ts, walls.ts, stickers.ts, paint.ts, model.ts)
    // Shared between about:blank (New Tab) and search://settings/themes (live miniature preview + 160x100 preset cards).
    public static class HomeThemesJs
    {
        private static string _cachedImgPath;
        private static DateTime _cachedImgWriteUtc;
        private static string _cachedImgDataUri;

        public static string SaveCustomImageCopy(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return "";
            string ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string dest = Path.Combine(Store.RootDir, "home-bg" + ext.ToLowerInvariant());
            File.Copy(sourcePath, dest, true);
            _cachedImgPath = null;
            return dest;
        }

        public static void RemoveCustomImageCopy()
        {
            try
            {
                if (!string.IsNullOrEmpty(Store.Prefs.HomeImagePath) &&
                    Store.Prefs.HomeImagePath.StartsWith(Store.RootDir, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Store.Prefs.HomeImagePath))
                {
                    File.Delete(Store.Prefs.HomeImagePath);
                }
            }
            catch { }
            _cachedImgPath = null;
            _cachedImgDataUri = "";
        }

        public static string GetCustomImageDataUri()
        {
            string path = Store.Prefs != null ? Store.Prefs.HomeImagePath : "";
            if (string.IsNullOrEmpty(path) && Store.Prefs != null && Store.Prefs.BlankWallMode == "Image")
                path = Store.Prefs.BlankWallCustomValue;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
            try
            {
                DateTime wt = File.GetLastWriteTimeUtc(path);
                if (_cachedImgPath == path && _cachedImgWriteUtc == wt && _cachedImgDataUri != null)
                    return _cachedImgDataUri;
                byte[] bytes = File.ReadAllBytes(path);
                string ext = (Path.GetExtension(path) ?? ".png").ToLowerInvariant();
                string mime = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg"
                    : ext == ".webp" ? "image/webp"
                    : ext == ".gif" ? "image/gif"
                    : ext == ".bmp" ? "image/bmp"
                    : "image/png";
                _cachedImgDataUri = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
                _cachedImgPath = path;
                _cachedImgWriteUtc = wt;
                return _cachedImgDataUri;
            }
            catch { return ""; }
        }

        public static readonly string RendererScript = @"
(function(){
  if (window.__searchThemes) return;
  const M64 = (1n << 64n) - 1n, TWO53 = 1n << 53n, TWO53_1 = TWO53 + 1n, PI = Math.PI;
  class Seeded {
    constructor(seed) { this.s = seed === 0n ? 0x9E3779B97F4A7C15n : seed & M64; }
    next() { let s = this.s; s ^= (s << 13n) & M64; s ^= s >> 7n; s ^= (s << 17n) & M64; this.s = s; return s; }
    below(n) {
      let m = this.next() * n;
      if ((m & M64) < n) { const t = ((1n << 64n) - n) % n; while ((m & M64) < t) m = this.next() * n; }
      return m >> 64n;
    }
    in(lo, hi) { const r = this.below(TWO53_1); if (r === TWO53) return hi; return (hi - lo) * (Number(r) * Math.pow(2, -53)) + lo; }
    int(n) { return Number(this.below(BigInt(n))); }
  }
  function seed(name) {
    let h = 0xcbf29ce484222325n;
    const bytes = new TextEncoder().encode(name);
    for (let i = 0; i < bytes.length; i++) h = ((h ^ BigInt(bytes[i])) * 0x100000001b3n) & M64;
    return h;
  }
  function dayNumber() {
    const now = new Date();
    return Math.round(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) / 86400000) + 719163;
  }
  function channels(hex) {
    const d = (hex || '').startsWith('#') ? hex.slice(1) : (hex || '');
    if (d.length !== 6 || !/^[0-9a-fA-F]{6}$/.test(d)) return [0.53, 0.53, 0.53];
    const n = parseInt(d, 16);
    return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
  }
  function css(c, alpha) {
    if (alpha === undefined) alpha = 1;
    const ch = v => Math.round(Math.max(0, Math.min(1, v)) * 255);
    return 'rgb(' + ch(c[0]) + ' ' + ch(c[1]) + ' ' + ch(c[2]) + ' / ' + Math.max(0, Math.min(1, alpha)) + ')';
  }
  function pen(dark, night) {
    return function(c, alpha) {
      let r = c[0], g = c[1], b = c[2];
      if (night && !dark) { r = r * 0.9 + 0.08; g = g * 0.9 + 0.08; b = b * 0.9 + 0.08; }
      const k = dark ? (night ? 0.8 : 0.42) : 1;
      return css([r * k, g * k, b * k], alpha === undefined ? 1 : alpha);
    };
  }
  function ellipse(p, x, y, w, h) { p.moveTo(x + w, y + h / 2); p.ellipse(x + w / 2, y + h / 2, Math.abs(w / 2), Math.abs(h / 2), 0, 0, PI * 2); p.closePath(); return p; }
  const ellipseIn = (x, y, w, h) => ellipse(new Path2D(), x, y, w, h);
  const rectIn = (x, y, w, h) => { const p = new Path2D(); p.rect(x, y, w, h); return p; };
  const K = { e: 1.52866483, a1: 0.86840694, a2: 1.08849296, b: 0.63149379, c: 0.07491100, d: 0.16906812, f: 0.37282383 };
  function roundedRect(p, x, y, w, h, radius) {
    if (w < 0) { x += w; w = -w; } if (h < 0) { y += h; h = -h; }
    const r = Math.max(0, Math.min(radius, w / 2, h / 2));
    if (r <= 0) { p.rect(x, y, w, h); return p; }
    const side = half => {
      if (half >= K.e * r) return [K.a1 * r, K.a2 * r, K.e * r];
      const t = (half / r - 1) / (K.e - 1);
      return [r * (0.82 + t * (K.a1 - 0.82)), r * (0.96 + t * (K.a2 - 0.96)), half];
    };
    const [hx1, hx2, hx] = side(w / 2), [vy1, vy2, vy] = side(h / 2);
    const X = x + w, Y = y + h, b = K.b * r, c = K.c * r, d = K.d * r, f = K.f * r;
    p.moveTo(X, y + h / 2);
    p.lineTo(X, Y - vy); p.bezierCurveTo(X, Y - vy2, X, Y - vy1, X - c, Y - b); p.bezierCurveTo(X - d, Y - f, X - f, Y - d, X - b, Y - c); p.bezierCurveTo(X - hx1, Y, X - hx2, Y, X - hx, Y);
    p.lineTo(x + hx, Y); p.bezierCurveTo(x + hx2, Y, x + hx1, Y, x + b, Y - c); p.bezierCurveTo(x + f, Y - d, x + d, Y - f, x + c, Y - b); p.bezierCurveTo(x, Y - vy1, x, Y - vy2, x, Y - vy);
    p.lineTo(x, y + vy); p.bezierCurveTo(x, y + vy2, x, y + vy1, x + c, y + b); p.bezierCurveTo(x + d, y + f, x + f, y + d, x + b, y + c); p.bezierCurveTo(x + hx1, y, x + hx2, y, x + hx, y);
    p.lineTo(X - hx, y); p.bezierCurveTo(X - hx2, y, X - hx1, y, X - b, y + c); p.bezierCurveTo(X - f, y + d, X - d, y + f, X - c, y + b); p.bezierCurveTo(X, y + vy1, X, y + vy2, X, y + vy);
    p.closePath(); return p;
  }
  const roundedRectIn = (x, y, w, h, r) => roundedRect(new Path2D(), x, y, w, h, r);
  const arcTo = (p, x, y, r, a, b) => { p.arc(x, y, r, a * PI / 180, b * PI / 180, false); return p; };
  const applying = (p, m) => { const q = new Path2D(); q.addPath(p, m); return q; };
  const fill = (g, path, style) => { g.fillStyle = style; g.fill(path); };
  const stroke = (g, path, style, width) => { g.strokeStyle = style; g.lineWidth = width; g.lineCap = 'round'; g.lineJoin = 'round'; g.stroke(path); };
  const linear = (g, x0, y0, x1, y1, cols) => { const grad = g.createLinearGradient(x0, y0, x1, y1); cols.forEach((c, i) => grad.addColorStop(cols.length === 1 ? 0 : i / (cols.length - 1), c)); return grad; };
  const scaleOf = g => { const m = g.getTransform(); return Math.hypot(m.a, m.b); };

  const WALLS = ['openRoad','desertHighway','coastalCliffs','mountainLake','riceTerraces','northernLights','saturnRise','moonscape','cityAtNight','monsoonForest'];
  const NIGHT = new Set(['northernLights','saturnRise','moonscape','cityAtNight']);
  const PACKS = [['space','Space'],['roadTrip','Road Trip'],['ocean','Ocean'],['jungle','Jungle'],['streetFood','Street Food'],['retroTech','Retro Tech'],['weather','Weather'],['music','Music'],['wonders','Wonders'],['search','Search']];

  class Scene {
    constructor(g, w, h, p, s) { this.g = g; this.w = w; this.h = h; this.pen = p; this.rng = new Seeded(s); }
    rand(lo, hi) { return this.rng.in(lo, hi); }
    pt(x, y) { return [x * this.w, y * this.h]; }
    poly(pts) { const p = new Path2D(); pts.forEach(([x, y], i) => (i === 0 ? p.moveTo(x * this.w, y * this.h) : p.lineTo(x * this.w, y * this.h))); p.closePath(); return p; }
    fill(path, c, a) { fill(this.g, path, this.pen(c, a === undefined ? 1 : a)); }
    stroke(path, c, width, a) { stroke(this.g, path, this.pen(c, a === undefined ? 1 : a), width); }
    disc(x, y, r) { const { w, h } = this; return ellipseIn(x * w - r * h, y * h - r * h, 2 * r * h, 2 * r * h); }
    oval(x, y, rx, ry) { const { w, h } = this; return ellipseIn((x - rx) * w, (y - ry) * h, 2 * rx * w, 2 * ry * h); }
    rect(x, y, rw, rh) { const { w, h } = this; return rectIn(x * w, y * h, rw * w, rh * h); }
    sky(cols, to) { if (to === undefined) to = 1; const { g, w, h } = this; fill(g, rectIn(-2, -2, w + 4, h + 4), linear(g, 0, 0, 0, h * to, cols.map(c => this.pen(c)))); }
    band(x, y, bw, bh, y0, y1, cols) { fill(this.g, rectIn(x, y, bw, bh), linear(this.g, 0, y0, 0, y1, cols)); }
    ridgeY(u, base, amp, freq, phase) { return base + amp * (0.6 * Math.sin(u * freq * 2 * PI + phase) + 0.4 * Math.sin(u * freq * 5.4 * PI / 2 + phase * 1.7)); }
    ridge(base, amp, freq, phase) {
      const { w, h } = this, p = new Path2D(); p.moveTo(-4, h * 1.05);
      for (let i = 0; i <= 64; i++) { const u = i / 64; p.lineTo(-4 + u * (w + 8), this.ridgeY(u, base, amp, freq, phase) * h); }
      p.lineTo(w + 4, h * 1.05); p.closePath(); return p;
    }
    stars(n, top, bottom) {
      const { w, h } = this, k = Math.min(w, h) / 900;
      for (let i = 0; i < n; i++) { const x = this.rand(0, 1) * w, y = this.rand(top, bottom) * h, r = this.rand(0.5, 1.6) * k; this.fill(ellipseIn(x - r, y - r, r * 2, r * 2), [1, 1, 0.92], this.rand(0.4, 0.95)); }
    }
    cloud(x, y, s, c, a) { this.fill(this.oval(x, y, 0.07 * s, 0.022 * s), c, a); this.fill(this.oval(x - 0.03 * s, y - 0.016 * s, 0.04 * s, 0.022 * s), c, a); this.fill(this.oval(x + 0.03 * s, y - 0.012 * s, 0.045 * s, 0.02 * s), c, a); }
    pine(x, base, height, c) {
      const { w, h } = this, tw = height * 0.16 * h / w;
      this.fill(this.rect(x - tw / 2, base - height * 0.12, tw, height * 0.14), c);
      for (let k = 0; k < 3; k++) { const top = base - height + k * height * 0.24, bot = base - height * 0.1 - (2 - k) * height * 0.2, half = (0.22 + 0.16 * k) * height * h / w * 0.9; this.fill(this.poly([[x, top], [x + half, bot], [x - half, bot]]), c); }
    }
    grain() {
      const { w, h } = this, k = Math.max(0.7, Math.min(w, h) / 900);
      for (let i = 0; i < 240; i++) { const x = this.rand(0, 1) * w, y = this.rand(0, 1) * h, r = this.rand(0.4, 1.0) * k; fill(this.g, ellipseIn(x, y, r * 2, r * 2), i % 2 === 0 ? 'rgb(0 0 0 / 0.035)' : 'rgb(255 255 255 / 0.05)'); }
    }
    openRoad() {
      const { w, h } = this;
      this.sky([[0.58,0.78,0.92],[0.80,0.90,0.94],[0.99,0.93,0.82]], 0.65);
      this.cloud(0.2,0.2,1.4,[1,1,1],0.7); this.cloud(0.78,0.14,1.1,[1,1,1],0.6); this.cloud(0.55,0.32,0.8,[1,1,1],0.45);
      this.fill(this.ridge(0.62,0.02,1.3,0.5),[0.66,0.78,0.68]); this.fill(this.ridge(0.645,0.016,1.9,2.0),[0.52,0.70,0.48]);
      const hz = 0.65; this.fill(this.rect(-0.01,hz,1.02,1-hz+0.02),[0.55,0.72,0.36]); const vp = this.pt(0.5,hz);
      const tones = [[0.62,0.77,0.38],[0.80,0.74,0.40],[0.48,0.66,0.34]];
      for (let i=-12;i<12;i++) { const x0=0.5+i*0.22,x1=x0+0.22,tri=new Path2D(); tri.moveTo(vp[0],vp[1]); tri.lineTo(...this.pt(x0+(x0-0.5)*3,1.05)); tri.lineTo(...this.pt(x1+(x1-0.5)*3,1.05)); tri.closePath(); this.fill(tri,tones[((i%3)+3)%3],i%2===0?0.9:0.5); }
      for (let k=0;k<9;k++) { const t0=Math.pow(k/9,2),t1=Math.pow((k+0.5)/9,2); this.fill(this.rect(-0.01,hz+(1-hz)*t0,1.02,(1-hz)*(t1-t0)),[1,1,0.8],0.1); }
      const road=new Path2D(); road.moveTo(vp[0]-0.004*w,vp[1]); road.lineTo(vp[0]+0.004*w,vp[1]); road.lineTo(...this.pt(0.67,1.02)); road.lineTo(...this.pt(0.33,1.02)); road.closePath(); this.fill(road,[0.42,0.42,0.46]);
      for (let k=0;k<14;k+=2) { const t0=Math.pow(k/14,2),t1=Math.pow((k+1)/14,2),w0=0.002+0.007*t0,w1=0.002+0.007*t1,y0=hz+(1.02-hz)*t0,y1=hz+(1.02-hz)*t1; this.fill(this.poly([[0.5-w0,y0],[0.5+w0,y0],[0.5+w1,y1],[0.5-w1,y1]]),[0.96,0.9,0.62]); }
      for (const side of [-1,1]) {
        const tops=[];
        for (let j=0;j<7;j++) { const t=Math.pow((j+1.3)/8.2,1.7),y=hz+(1.02-hz)*t,x=0.5+side*(0.004+0.166*t+0.03*t+0.008),ph=0.2*t; this.fill(this.rect(x-0.0015-0.002*t,y-ph,0.003+0.004*t,ph),[0.30,0.24,0.22]); this.fill(this.rect(x-0.008*(0.4+t),y-ph,0.016*(0.4+t),0.004+0.006*t),[0.30,0.24,0.22]); tops.push(this.pt(x,y-ph)); }
        for (let j=0;j<tops.length-1;j++) { const wire=new Path2D(); wire.moveTo(...tops[j]); wire.quadraticCurveTo((tops[j][0]+tops[j+1][0])/2,(tops[j][1]+tops[j+1][1])/2+0.012*h,...tops[j+1]); this.stroke(wire,[0.28,0.24,0.24],1,0.7); }
      }
    }
    mesa(cx,wd,ht,base) { return this.poly([[-0.5,0],[-0.46,-0.55],[-0.40,-0.58],[-0.38,-1],[0.30,-1],[0.33,-0.6],[0.40,-0.55],[0.5,0]].map(([a,b])=>[cx+a*wd,base+b*ht])); }
    desertHighway() {
      const { w, h } = this, hz = 0.67;
      this.sky([[0.26,0.18,0.44],[0.62,0.30,0.50],[0.94,0.52,0.44],[0.99,0.76,0.46]], hz);
      for (const [k,a] of [[1.9,0.12],[1.45,0.2],[0.98,0.9]]) this.fill(this.disc(0.74,hz,0.12*k),[1,0.82,0.5],a);
      this.fill(this.disc(0.74,hz,0.12),[1,0.88,0.55]);
      for (const [cx,wd,ht] of [[0.12,0.2,0.14],[0.34,0.14,0.09],[0.9,0.2,0.15],[0.64,0.1,0.05]]) this.fill(this.mesa(cx,wd,ht,hz+0.01),[0.62,0.34,0.50]);
      for (const [cx,wd,ht] of [[0.04,0.26,0.2],[0.23,0.15,0.1],[0.97,0.22,0.17],[0.8,0.12,0.07]]) this.fill(this.mesa(cx,wd,ht,hz+0.02),[0.74,0.36,0.34]);
      this.band(-2,hz*h,w+4,h,hz*h,h,[this.pen([0.72,0.36,0.32]),this.pen([0.40,0.19,0.28])]);
      const centre = t => [(0.5+Math.sin(t*PI*1.6)*0.17*t)*w, (hz+(1.03-hz)*Math.pow(t,1.6))*h], half = t => (0.003+0.13*t)*w;
      const left=[], right=[];
      for (let i=0;i<=40;i++) { const t=i/40,c=centre(t),hw=half(t); left.push([c[0]-hw,c[1]]); right.push([c[0]+hw,c[1]]); }
      const road=new Path2D(); road.moveTo(...left[0]); left.slice(1).forEach(q=>road.lineTo(...q)); right.slice().reverse().forEach(q=>road.lineTo(...q)); road.closePath(); this.fill(road,[0.27,0.19,0.30]);
      for (let k=1;k<20;k+=2) { const a=centre(k/20),b=centre((k+1)/20),d0=half(k/20)*0.07,d1=half((k+1)/20)*0.07,dash=new Path2D(); dash.moveTo(a[0]-d0,a[1]); dash.lineTo(a[0]+d0,a[1]); dash.lineTo(b[0]+d1,b[1]); dash.lineTo(b[0]-d1,b[1]); dash.closePath(); this.fill(dash,[0.98,0.80,0.50]); }
      for (const [x,y,k] of [[0.07,0.93,1.0],[0.14,0.86,0.6],[0.92,0.9,0.8]]) {
        const c=[0.20,0.10,0.20], bw=0.012*k;
        this.fill(roundedRectIn((x-bw)*w,(y-0.2*k)*h,bw*2*w,0.2*k*h,bw*w),c);
        const arms=new Path2D(); arms.moveTo(...this.pt(x-bw,y-0.07*k)); arms.lineTo(...this.pt(x-0.035*k,y-0.07*k)); arms.lineTo(...this.pt(x-0.035*k,y-0.13*k)); arms.moveTo(...this.pt(x+bw,y-0.1*k)); arms.lineTo(...this.pt(x+0.035*k,y-0.1*k)); arms.lineTo(...this.pt(x+0.035*k,y-0.16*k)); this.stroke(arms,c,0.014*w*k);
      }
    }
    cliff(side,top,width,steps) {
      const x = f => (side < 0 ? f : 1 - f), pts = [[x(-0.02),1.05],[x(-0.02),top],[x(width),top+0.015]];
      for (let i=1;i<=steps;i++) { const f=i/steps,j=this.rand(-0.02,0.02); pts.push([x(width*(1-0.5*f)+j),top+(0.94-top)*(f-0.5/steps)]); pts.push([x(width*(1-0.5*f)-0.015+j),top+(0.94-top)*f]); }
      pts.push([x(0.04),1.05]); return this.poly(pts);
    }
    coastalCliffs() {
      const { w, h } = this, hz = 0.56;
      this.sky([[0.68,0.84,0.95],[0.88,0.93,0.94],[0.99,0.92,0.84]], hz);
      this.fill(this.disc(0.58,0.26,0.05),[1,0.96,0.84],0.7); this.cloud(0.3,0.16,1.2,[1,1,1],0.7); this.cloud(0.8,0.28,0.9,[1,1,1],0.55);
      this.band(-2,hz*h,w+4,h,hz*h,h,[this.pen([0.46,0.72,0.80]),this.pen([0.20,0.46,0.60])]);
      this.fill(this.ridge(0.55,0.008,2.2,1.0),[0.62,0.76,0.78],0.8);
      for (let i=0;i<16;i++) { const y=this.rand(0.6,0.96),x=this.rand(0.1,0.8),l=this.rand(0.03,0.1); this.fill(roundedRectIn(x*w,y*h,l*w,0.003*h+1,2),[1,1,1],0.28); }
      this.fill(this.cliff(-1,0.52,0.3,4),[0.62,0.58,0.60]); this.fill(this.cliff(1,0.52,0.27,4),[0.62,0.58,0.60]);
      this.fill(this.rect(0.88,0.505,0.12,0.02),[0.50,0.66,0.44]);
      const lx=0.9, base=0.515;
      this.fill(this.poly([[lx,0.35],[0.62,0.37],[0.62,0.43]]),[1,0.95,0.65],0.2);
      this.fill(this.poly([[lx-0.016,base],[lx-0.009,0.34],[lx+0.009,0.34],[lx+0.016,base]]),[0.97,0.95,0.92]);
      this.fill(this.poly([[lx-0.0135,0.4],[lx-0.0115,0.37],[lx+0.0115,0.37],[lx+0.0135,0.4]]),[0.84,0.30,0.28]);
      this.fill(this.poly([[lx-0.0155,0.465],[lx-0.0135,0.435],[lx+0.0135,0.435],[lx+0.0155,0.465]]),[0.84,0.30,0.28]);
      this.fill(this.rect(lx-0.012,0.325,0.024,0.016),[1,0.9,0.5]);
      this.fill(this.poly([[lx-0.013,0.325],[lx,0.30],[lx+0.013,0.325]]),[0.30,0.28,0.34]);
      this.fill(this.cliff(-1,0.64,0.22,5),[0.46,0.40,0.42]); this.fill(this.cliff(1,0.7,0.2,5),[0.44,0.38,0.41]);
      this.fill(this.rect(-0.01,0.635,0.22,0.016),[0.44,0.62,0.40]); this.fill(this.rect(0.8,0.695,0.21,0.016),[0.44,0.62,0.40]);
      for (const [x,y] of [[0.2,0.9],[0.12,0.96],[0.84,0.92],[0.92,0.97]]) this.fill(this.oval(x,y,0.05,0.012),[1,1,1],0.5);
    }
    peak(cx,wd,ph,wh) { return this.poly([[cx-wd,wh],[cx-wd*0.4,wh-ph*0.62],[cx-wd*0.12,wh-ph*0.78],[cx,wh-ph],[cx+wd*0.2,wh-ph*0.72],[cx+wd*0.5,wh-ph*0.5],[cx+wd,wh]]); }
    cap(cx,wd,ph,wh) { return this.poly([[cx-wd*0.3,wh-ph*0.64],[cx-wd*0.12,wh-ph*0.78],[cx,wh-ph],[cx+wd*0.2,wh-ph*0.72],[cx+wd*0.28,wh-ph*0.62],[cx+wd*0.12,wh-ph*0.68],[cx,wh-ph*0.6],[cx-wd*0.1,wh-ph*0.68]]); }
    mountainLake() {
      const { g, w, h } = this, wh = 0.62;
      this.sky([[0.72,0.80,0.95],[0.97,0.86,0.82],[0.99,0.90,0.78]], wh);
      const far=[[0.14,0.3,0.3],[0.45,0.28,0.15],[0.8,0.32,0.3]], near=[[0.03,0.28,0.34],[0.3,0.24,0.2],[0.7,0.22,0.17],[0.96,0.3,0.4]], farC=[0.62,0.68,0.84], nearC=[0.38,0.48,0.64];
      far.forEach(p=>this.fill(this.peak(p[0],p[1],p[2],wh),farC)); far.forEach(p=>this.fill(this.cap(p[0],p[1],p[2],wh),[0.96,0.96,1]));
      near.forEach(p=>this.fill(this.peak(p[0],p[1],p[2],wh),nearC)); near.forEach(p=>this.fill(this.cap(p[0],p[1],p[2],wh),[1,1,1]));
      this.band(-2,wh*h,w+4,h,wh*h,h,[this.pen([0.58,0.72,0.82]),this.pen([0.32,0.50,0.64])]);
      g.save(); g.clip(rectIn(0,wh*h,w,h)); g.translate(0,2*wh*h); g.scale(1,-1);
      far.forEach(p=>fill(g,this.peak(p[0],p[1],p[2],wh),this.pen(farC,0.45)));
      near.forEach(p=>fill(g,this.peak(p[0],p[1],p[2],wh),this.pen(nearC,0.5)));
      near.forEach(p=>fill(g,this.cap(p[0],p[1],p[2],wh),this.pen([1,1,1],0.3)));
      g.restore();
      for (let i=0;i<14;i++) { const y=this.rand(wh+0.03,0.95),x=this.rand(0.1,0.8),l=this.rand(0.04,0.12); this.fill(roundedRectIn(x*w,y*h,l*w,0.003*h+1,2),[1,1,1],0.3); }
      const shore=[0.20,0.34,0.30]; this.fill(this.ridge(0.94,0.02,1.4,0.3),shore);
      for (const [x,hgt,b] of [[0.03,0.34,0.95],[0.09,0.26,0.96],[0.16,0.2,0.94],[0.22,0.1,0.9],[0.97,0.36,0.95],[0.9,0.28,0.96],[0.83,0.2,0.94],[0.77,0.09,0.9]]) this.pine(x,b,hgt,shore);
      this.pine(0.04,0.66,0.1,[0.28,0.40,0.38]); this.pine(0.95,0.66,0.09,[0.28,0.40,0.38]);
    }
    riceTerraces() {
      const { w, h } = this, mist=[1,1,0.97], greens=[[0.50,0.70,0.40],[0.62,0.78,0.46],[0.42,0.62,0.38],[0.70,0.80,0.50]];
      this.sky([[0.84,0.92,0.90],[0.97,0.95,0.88]],0.5);
      this.fill(this.ridge(0.30,0.05,1.1,0.4),[0.74,0.84,0.82]); this.fill(this.ridge(0.37,0.05,1.6,2.2),[0.64,0.78,0.74]); this.fill(this.ridge(0.43,0.04,2.1,4.1),[0.54,0.72,0.62]);
      this.band(0,0.3*h,w,0.24*h,0.3*h,0.54*h,[this.pen(mist,0),this.pen(mist,0.5),this.pen(mist,0)]);
      for (let k=0;k<10;k++) { const path=this.ridge(0.5+k*0.058,0.012+0.004*k,0.9+0.2*(k%3),k*1.3); this.fill(path,greens[k%4]); this.stroke(path,[0.86,0.94,0.80],1.6,0.6); }
      this.band(0,0.46*h,w,0.14*h,0.46*h,0.6*h,[this.pen(mist,0),this.pen(mist,0.28),this.pen(mist,0)]);
    }
    northernLights() {
      const { g, w, h } = this;
      this.sky([[0.02,0.05,0.15],[0.05,0.14,0.26],[0.08,0.24,0.32]],0.8); this.stars(130,0,0.6);
      const ribbons=[[0.16,0.1,1.3,0.4,[0.35,0.95,0.65]],[0.26,0.12,1.7,2.3,[0.30,0.85,0.85]],[0.12,0.08,2.1,4.4,[0.55,0.45,0.92]],[0.32,0.09,1.2,1.1,[0.40,0.92,0.55]]];
      g.save(); g.filter = 'blur(' + (Math.min(w,h)*0.014*scaleOf(g)) + 'px)';
      for (const [y0,ht,f,ph,c] of ribbons) {
        const p=new Path2D();
        for (let i=0;i<=60;i++) { const u=i/60,y=(y0+0.07*Math.sin(u*f*2*PI+ph))*h; i===0?p.moveTo(u*w,y):p.lineTo(u*w,y); }
        for (let i=60;i>=0;i--) { const u=i/60,top=y0+0.07*Math.sin(u*f*2*PI+ph); p.lineTo(u*w,(top+ht*(0.7+0.3*Math.sin(u*9+ph)))*h); }
        p.closePath(); fill(g,p,linear(g,0,(y0-0.07)*h,0,(y0+ht+0.07)*h,[this.pen(c,0),this.pen(c,0.6),this.pen([0.3,0.8,0.7],0)]));
      }
      g.restore();
      this.fill(this.ridge(0.68,0.05,1.2,1.0),[0.30,0.40,0.56]); this.fill(this.ridge(0.79,0.035,1.7,3.2),[0.72,0.80,0.92]);
      const cx=0.8, cy=this.ridgeY(0.8,0.79,0.035,1.7,3.2);
      this.fill(this.rect(cx-0.028,cy-0.05,0.056,0.058),[0.30,0.20,0.20]);
      this.fill(this.poly([[cx-0.036,cy-0.048],[cx,cy-0.095],[cx+0.036,cy-0.048]]),[0.20,0.14,0.16]);
      this.fill(this.rect(cx+0.012,cy-0.095,0.01,0.03),[0.20,0.14,0.16]);
      this.fill(this.disc(cx-0.005,cy-0.028,0.026),[1,0.85,0.4],0.25);
      this.fill(this.rect(cx-0.014,cy-0.036,0.018,0.02),[1,0.85,0.45]);
      this.pine(cx-0.07,cy+0.01,0.1,[0.14,0.22,0.26]); this.pine(cx+0.075,cy+0.01,0.13,[0.14,0.22,0.26]);
      this.fill(this.ridge(0.92,0.025,1.3,5.0),[0.86,0.92,1]);
    }
    ringPath(front,c,rx,ry,tilt) { return applying(arcTo(new Path2D(),0,0,1,front?0:180,front?180:360), new DOMMatrix().translate(c[0],c[1]).rotate(tilt*180/PI).scale(rx,ry)); }
    saturnRise() {
      const { g, h } = this;
      this.sky([[0.04,0.03,0.14],[0.20,0.10,0.30],[0.52,0.24,0.36]],0.82); this.stars(170,0,0.75);
      this.fill(this.disc(0.16,0.24,0.035),[0.72,0.68,0.76]); this.fill(this.disc(0.15,0.235,0.008),[0.55,0.52,0.6],0.8);
      const c=this.pt(0.7,0.66), r=0.2*h, tilt=-0.3, rw=r*0.1;
      this.stroke(this.ringPath(false,c,r*2.1,r*0.5,tilt),[0.92,0.82,0.64],rw); this.stroke(this.ringPath(false,c,r*1.7,r*0.4,tilt),[0.80,0.68,0.52],rw*0.5);
      this.fill(ellipseIn(c[0]-r,c[1]-r,2*r,2*r),[0.94,0.80,0.56]);
      g.save(); g.clip(ellipseIn(c[0]-r,c[1]-r,2*r,2*r));
      [-0.6,-0.25,0.05,0.4].forEach((f,i)=>fill(g,rectIn(c[0]-r,c[1]+f*r,2*r,r*(i%2===0?0.2:0.12)),this.pen([0.78,0.54,0.38],0.45)));
      fill(g,ellipseIn(c[0]-r*1.45,c[1]-r*1.1,2*r,2*r),this.pen([0.2,0.08,0.2],0.35)); g.restore();
      this.stroke(this.ringPath(true,c,r*2.1,r*0.5,tilt),[0.92,0.82,0.64],rw); this.stroke(this.ringPath(true,c,r*1.7,r*0.4,tilt),[0.80,0.68,0.52],rw*0.5);
      this.fill(this.ridge(0.8,0.03,2.4,0.7),[0.30,0.14,0.30]);
      for (let i=0;i<14;i++) { const x=this.rand(0,1),y=this.ridgeY(x,0.8,0.03,2.4,0.7),s=this.rand(0.012,0.03); this.fill(this.poly([[x-s,y+0.01],[x-s*0.3,y-s*1.6],[x+s*0.5,y-s],[x+s,y+0.01]]),[0.30,0.14,0.30]); }
      this.fill(this.ridge(0.9,0.025,1.6,2.5),[0.15,0.07,0.20]);
      for (let i=0;i<10;i++) { const x=this.rand(0,1),y=this.ridgeY(x,0.9,0.025,1.6,2.5),s=this.rand(0.015,0.04); this.fill(this.poly([[x-s,y+0.01],[x-s*0.5,y-s*1.2],[x+s*0.3,y-s*1.5],[x+s,y+0.01]]),[0.15,0.07,0.20]); }
    }
    moonscape() {
      const { g, w, h } = this;
      this.sky([[0.02,0.03,0.09],[0.07,0.08,0.18],[0.14,0.14,0.24]],0.72); this.stars(160,0,0.66);
      const c=this.pt(0.78,0.27), r=0.09*h, earth=ellipseIn(c[0]-r,c[1]-r,2*r,2*r);
      this.stroke(ellipseIn(c[0]-r*1.08,c[1]-r*1.08,2.16*r,2.16*r),[0.55,0.75,1],2,0.4); this.fill(earth,[0.22,0.46,0.84]);
      g.save(); g.clip(earth);
      [[-0.3,-0.2,0.5,0.35],[0.35,0.25,0.4,0.5],[-0.1,0.45,0.3,0.2]].forEach(([dx,dy,sx,sy])=>fill(g,ellipseIn(c[0]+(dx-sx/2)*r,c[1]+(dy-sy/2)*r,sx*r,sy*r),this.pen([0.40,0.68,0.40])));
      [[-0.1,-0.5],[0.3,0],[-0.4,0.3]].forEach(([dx,dy])=>fill(g,ellipseIn(c[0]+dx*r-0.4*r,c[1]+dy*r-0.1*r,0.8*r,0.2*r),this.pen([1,1,1],0.55)));
      fill(g,ellipseIn(c[0]-r*1.5,c[1]-r*0.8,2*r,2*r),this.pen([0.02,0.03,0.1],0.45)); g.restore();
      this.fill(this.ridge(0.68,0.012,2.6,0.5),[0.46,0.46,0.52]);
      fill(g,this.ridge(0.73,0.014,1.5,2.2),linear(g,0,0.73*h,0,h,[this.pen([0.70,0.70,0.74]),this.pen([0.50,0.50,0.56])]));
      for (let i=0;i<26;i++) {
        const x=this.rand(0.02,0.98),y=this.rand(0.76,0.98); if (Math.abs(x-0.5)<0.2&&y<0.86) continue;
        const rx=this.rand(0.025,0.08)*(0.6+(y-0.75)),ry=rx*w/h*0.22;
        this.fill(this.oval(x,y,rx,ry),[0.40,0.40,0.46],0.8); this.fill(this.oval(x,y+ry*0.18,rx*0.88,ry*0.8),[0.50,0.50,0.56],0.7); this.stroke(this.oval(x,y,rx,ry),[0.84,0.84,0.88],1.2,0.5);
      }
    }
    cityAtNight() {
      const { g, w, h } = this;
      this.sky([[0.08,0.08,0.28],[0.34,0.20,0.50],[0.80,0.46,0.52],[0.98,0.66,0.50]],0.86); this.stars(70,0,0.45);
      const mc=this.pt(0.78,0.2), mr=0.06*h;
      this.fill(ellipseIn(mc[0]-mr*2.2,mc[1]-mr*2.2,mr*4.4,mr*4.4),[1,0.95,0.7],0.1);
      g.save(); const out=new Path2D(); out.rect(-w,-h,3*w,3*h); ellipse(out,mc[0]-mr*0.55,mc[1]-mr*1.2,mr*2,mr*2); g.clip(out,'evenodd');
      fill(g,ellipseIn(mc[0]-mr,mc[1]-mr,mr*2,mr*2),this.pen([1,0.95,0.72])); g.restore();
      [[0.84,0,[0.34,0.26,0.54],false],[0.90,0,[0.20,0.16,0.38],true],[1.0,0,[0.10,0.08,0.22],true]].forEach((layer,li)=>{
        let x=-0.01;
        while (x<1.01) {
          const bw=this.rand(0.03,0.065), edge=Math.abs(x+bw/2-0.5)*2, ht=(0.05+this.rand(0,0.05))+(0.05+this.rand(0,0.2))*edge*edge*(1.2-li*0.2), top=layer[0]-ht;
          this.fill(this.rect(x,top,bw,ht+0.2),layer[2]);
          if (this.rand(0,1)<0.25) this.fill(this.rect(x+bw/2-0.001,top-0.03,0.002,0.03),layer[2]);
          if (layer[3]) {
            const cols=Math.max(1,Math.trunc(bw/0.011)), rows=Math.max(1,Math.trunc(ht/0.02));
            for (let cI=0;cI<cols;cI++) for (let rI=0;rI<rows;rI++) if (this.rand(0,1)<0.4) this.fill(this.rect(x+0.006+cI*0.011,top+0.008+rI*0.02,0.005,0.01),[1,0.86,0.46],0.85);
          }
          x += bw + this.rand(0,0.004);
        }
      });
    }
    monsoonForest() {
      const { w, h } = this, tones=[[0.60,0.72,0.66],[0.44,0.62,0.52],[0.32,0.52,0.42],[0.20,0.38,0.30],[0.11,0.25,0.20]], bases=[0.44,0.54,0.64,0.75,0.88], haze=[0.92,0.96,0.94];
      this.sky([[0.66,0.74,0.72],[0.84,0.89,0.85]],0.6);
      bases.forEach((base,i)=>{
        const n=26+i*2, lift=i>=3?0.3:0.05;
        for (let j=0;j<=n;j++) { const u=j/n,edge=Math.pow(Math.abs(u-0.5)*2,2),y=base-lift*edge+this.rand(-0.015,0.015),rr=this.rand(0.045,0.085)*(0.8+0.1*i); this.fill(this.oval(u,y,rr,rr*w/h*0.8*0.9),tones[i]); }
        this.fill(this.rect(-0.01,base+0.02,1.02,1.1-base),tones[i]);
        if (i<4) this.band(0,(base-0.05)*h,w,0.2*h,(base-0.05)*h,(base+0.15)*h,[this.pen(haze,0),this.pen(haze,0.42),this.pen(haze,0)]);
      });
      for (const x of [0.05,0.12,0.9,0.96]) { const p=new Path2D(); p.moveTo(...this.pt(x,0.35)); p.lineTo(...this.pt(x+this.rand(-0.02,0.02),1.02)); this.stroke(p,[0.08,0.18,0.15],0.012*w,0.8); }
      for (let i=0;i<260;i++) { const x=this.rand(0,1.05)*w,y=this.rand(-0.05,1)*h,l=this.rand(0.03,0.06)*h,p=new Path2D(); p.moveTo(x,y); p.lineTo(x-l*0.16,y+l); this.stroke(p,[0.92,0.96,0.98],0.9,0.3); }
    }
  }

  // Stickers engine (exact 1:1 port of stickers.ts)
  const f = (path, colour) => ({ path, colour, width: 0 });
  const s = (path, colour, width) => ({ path, colour, width: width === undefined ? 0.1 : width });
  const disc = (x,y,r) => ellipseIn(x-r,y-r,r*2,r*2);
  const oval = (x,y,rx,ry) => ellipseIn(x-rx,y-ry,rx*2,ry*2);
  const box = (x,y,w,h,r) => roundedRectIn(x-w/2,y-h/2,w,h,r||0);
  function poly(v, closed) { if (closed===undefined) closed=true; const p=new Path2D(); for(let i=0;i+1<v.length;i+=2)(i===0?p.moveTo(v[i],v[i+1]):p.lineTo(v[i],v[i+1])); if(closed) p.closePath(); return p; }
  function arc(x,y,r,a,b,close) { const p=arcTo(new Path2D(),x,y,r,a,b); if(close) p.closePath(); return p; }
  function star(n,outer,inner,cx,cy) { cx=cx||0; cy=cy||0; const p=new Path2D(); for(let k=0;k<n*2;k++){ const a=-PI/2+k*PI/n,r=k%2===0?outer:inner,x=cx+r*Math.cos(a),y=cy+r*Math.sin(a); k===0?p.moveTo(x,y):p.lineTo(x,y); } p.closePath(); return p; }
  function sparkle(cx,cy,k) { const p=new Path2D(),d=0.13*k; p.moveTo(cx,cy-k); p.quadraticCurveTo(cx+d,cy-d,cx+k,cy); p.quadraticCurveTo(cx+d,cy+d,cx,cy+k); p.quadraticCurveTo(cx-d,cy+d,cx-k,cy); p.quadraticCurveTo(cx-d,cy-d,cx,cy-k); p.closePath(); return p; }
  function rays(n,r0,r1) { const p=new Path2D(); for(let k=0;k<n;k++){ const a=k*2*PI/n; p.moveTo(r0*Math.cos(a),r0*Math.sin(a)); p.lineTo(r1*Math.cos(a),r1*Math.sin(a)); } return p; }
  const turned = (p,a) => applying(p, new DOMMatrix().rotate(a*180/PI));
  const LOOKS = {
    space:{ground:[0.88,0.86,0.97],palette:[[0.42,0.40,0.85],[0.98,0.80,0.32],[0.35,0.66,0.88],[0.90,0.50,0.72],[0.98,0.58,0.40]]},
    roadTrip:{ground:[0.98,0.93,0.82],palette:[[0.30,0.62,0.62],[0.97,0.78,0.30],[0.90,0.40,0.35],[0.45,0.70,0.45],[0.30,0.35,0.55]]},
    ocean:{ground:[0.84,0.93,0.96],palette:[[0.30,0.60,0.85],[0.98,0.55,0.50],[0.98,0.82,0.45],[0.35,0.75,0.70],[0.25,0.35,0.60]]},
    jungle:{ground:[0.88,0.95,0.84],palette:[[0.35,0.65,0.40],[0.20,0.45,0.35],[0.98,0.82,0.35],[0.95,0.45,0.35],[0.30,0.60,0.85]]},
    streetFood:{ground:[0.99,0.91,0.80],palette:[[0.97,0.65,0.30],[0.90,0.35,0.30],[0.98,0.84,0.50],[0.50,0.75,0.45],[0.60,0.40,0.30]]},
    retroTech:{ground:[0.92,0.90,0.84],palette:[[0.35,0.65,0.70],[0.90,0.40,0.60],[0.98,0.80,0.35],[0.55,0.45,0.80],[0.25,0.28,0.45]]},
    weather:{ground:[0.88,0.94,0.98],palette:[[0.50,0.74,0.95],[0.99,0.80,0.30],[0.62,0.70,0.82],[0.96,0.50,0.45],[0.40,0.38,0.72]]},
    music:{ground:[0.96,0.90,0.93],palette:[[0.92,0.42,0.45],[0.60,0.45,0.85],[0.98,0.80,0.35],[0.35,0.72,0.70],[0.25,0.22,0.38]]},
    wonders:{ground:[0.97,0.92,0.84],palette:[[0.93,0.75,0.45],[0.85,0.35,0.30],[0.30,0.65,0.65],[0.62,0.60,0.76],[0.50,0.34,0.26],[0.80,0.60,0.32]]},
    search:{ground:[0.90,0.92,0.98],palette:[[0.35,0.55,0.95],[0.98,0.60,0.30],[0.40,0.80,0.65],[0.65,0.50,0.90],[0.95,0.50,0.60],[0.80,0.80,0.88]]}
  };
  const ringHalf = front => turned(applying(arc(0,0,1,front?0:180,front?180:360), new DOMMatrix().scale(1.05,0.3)), -0.4);
  const ART = {
    space:()=>[
      [s(ringHalf(false),1,0.13),f(disc(0,0,0.62),0),s(arc(0,0,0.45,200,300),3,0.08),s(ringHalf(true),1,0.13)],
      [f(poly([-0.2,0.55,0,1.1,0.2,0.55]),1),f(poly([-0.35,0.6,-0.35,-0.2,0,-1,0.35,-0.2,0.35,0.6]),-2),f(poly([-0.35,0.15,-0.75,0.75,-0.35,0.6]),4),f(poly([0.35,0.15,0.75,0.75,0.35,0.6]),4),f(poly([-0.35,-0.2,0,-1,0.35,-0.2]),4),f(disc(0,0,0.2),2),s(disc(0,0,0.2),0,0.06)],
      [f(disc(0,0,0.85),-2),s(disc(0,0,0.85),0,0.1),f(box(0,0.05,1.1,0.7,0.3),0),f(oval(-0.3,-0.12,0.2,0.1),2),f(disc(-0.95,0.1,0.12),1),f(disc(0.95,0.1,0.12),1)],
      [f(star(5,1,0.45),1),f(disc(-0.15,-0.1,0.07),-1),f(disc(0.15,-0.1,0.07),-1)],
      [f(poly([0.05,-0.6,0.6,-0.05,-0.9,0.9]),1),s(poly([-0.2,0.2,0.3,-0.3],false),3,0.08),f(disc(0.35,-0.35,0.38),4)],
      [f(arc(0,-0.1,0.45,180,360,true),2),f(oval(0,0.15,0.95,0.3),0),f(disc(-0.5,0.2,0.07),1),f(disc(0,0.28,0.07),1),f(disc(0.5,0.2,0.07),1)]
    ],
    roadTrip:()=>[
      [f(box(0,0.05,1.8,0.95,0.28),0),f(box(0,0.32,1.8,0.4,0.14),1),f(box(-0.4,-0.15,0.55,0.38,0.08),-2),f(box(0.35,-0.15,0.45,0.38,0.08),-2),f(disc(-0.55,0.56,0.22),-1),f(disc(0.55,0.56,0.22),-1),f(disc(-0.55,0.56,0.08),-2),f(disc(0.55,0.56,0.08),-2)],
      [f(disc(0,-0.25,0.6),2),f(poly([-0.5,0.05,0,0.95,0.5,0.05]),2),f(disc(0,-0.25,0.22),-2)],
      [f(box(0,0.7,0.12,0.9),4),f(poly([0,-0.95,0.8,-0.2,0,0.55,-0.8,-0.2]),1),s(poly([-0.3,-0.05,0,-0.4,0.3,-0.05],false),-1,0.13)],
      [f(disc(0,0,0.5),1),s(rays(8,0.7,0.97),1,0.15)],
      [f(box(0,0.1,0.42,1.6,0.21),3),s(poly([-0.2,0.2,-0.6,0.2,-0.6,-0.25],false),3,0.2),s(poly([0.2,-0.05,0.6,-0.05,0.6,-0.5],false),3,0.2),f(box(0,0.88,0.7,0.3,0.06),2)],
      [f(box(0,0.1,1.0,1.3,0.12),2),s(poly([-0.35,-0.55,-0.35,-0.85,0.1,-0.85,0.1,-0.55],false),2,0.12),f(poly([0.3,-0.5,0.55,-0.9,0.75,-0.8,0.6,-0.5]),2),s(poly([-0.3,-0.2,0.3,0.5],false),-2,0.12),s(poly([0.3,-0.2,-0.3,0.5],false),-2,0.12)]
    ],
    ocean:()=>{
      const wave=new Path2D(); wave.moveTo(-0.95,0.65); wave.bezierCurveTo(-0.95,-0.1,-0.4,-0.85,0.15,-0.85); wave.bezierCurveTo(0.6,-0.9,0.9,-0.55,0.7,-0.2); wave.quadraticCurveTo(0.6,-0.5,0.3,-0.4); wave.quadraticCurveTo(0.1,-0.1,0.4,0.2); wave.lineTo(0.95,0.65); wave.closePath();
      const ribs=new Path2D(); for(let k=1;k<=5;k++){ const a=180+k*30; ribs.moveTo(0,0.35); ribs.lineTo(0.85*Math.cos(a*PI/180),0.35+0.85*Math.sin(a*PI/180)); }
      return [
        [f(wave,0),s(poly([-0.75,0.4,-0.3,0.25,0.1,0.4,0.55,0.28,0.85,0.42],false),-2,0.1)],
        [f(arc(0,0.35,0.9,180,360,true),1),f(box(0,0.5,0.45,0.3,0.1),1),s(ribs,-2,0.07)],
        [f(oval(-0.1,0,0.7,0.45),3),f(poly([0.5,0,0.95,-0.4,0.95,0.4]),3),f(poly([-0.1,-0.4,0.1,-0.78,0.3,-0.38]),4),f(disc(-0.5,-0.1,0.09),-2),f(disc(-0.5,-0.1,0.045),-1),s(poly([0.1,-0.35,0.1,0.35],false),-2,0.08)],
        [s(disc(0,-0.72,0.17),4,0.1),s(poly([0,-0.55,0,0.8],false),4,0.14),s(poly([-0.4,-0.3,0.4,-0.3],false),4,0.14),s(arc(0,0.1,0.75,20,160),4,0.14)],
        [f(poly([-0.3,0.9,-0.2,-0.5,0.2,-0.5,0.3,0.9]),-2),f(poly([-0.27,0.35,-0.24,0,0.24,0,0.27,0.35]),1),f(box(0,-0.55,0.6,0.1),4),f(box(0,-0.75,0.3,0.25),2),f(poly([-0.25,-0.88,0,-1.08,0.25,-0.88]),4),f(box(0,0.93,0.8,0.14,0.05),4)],
        [f(star(5,1,0.5),1),f(disc(0,0,0.1),2),f(disc(0,-0.5,0.06),2),f(disc(0.45,-0.15,0.06),2),f(disc(0.28,0.4,0.06),2),f(disc(-0.28,0.4,0.06),2),f(disc(-0.45,-0.15,0.06),2)]
      ];
    },
    jungle:()=>{
      const banana=new Path2D(); banana.moveTo(-0.85,-0.3); banana.quadraticCurveTo(0,1.2,0.85,-0.6); banana.quadraticCurveTo(0,0.45,-0.85,-0.3);
      const beak=new Path2D(); beak.moveTo(-0.1,-0.6); beak.quadraticCurveTo(0.8,-0.7,0.95,0.1); beak.quadraticCurveTo(0.5,0.5,-0.1,0.25); beak.closePath();
      const petals=new Path2D(); for(let k=0;k<5;k++){ const a=k*2*PI/5-PI/2; petals.addPath(disc(0.55*Math.cos(a),0.55*Math.sin(a),0.4)); }
      return [
        [s(poly([0,0.2,0,1],false),1,0.12),f(disc(0,-0.05,0.9),0),f(poly([-1,-0.35,-0.35,-0.05,-1,0.05]),-3),f(poly([1,-0.35,0.35,-0.05,1,0.05]),-3),f(poly([-0.95,0.35,-0.3,0.2,-0.75,0.6]),-3),f(poly([0.95,0.35,0.3,0.2,0.75,0.6]),-3),s(poly([0,0.7,0,-0.8],false),1,0.07)],
        [f(poly([-0.2,0.7,-0.5,1.1,0.05,1.05,0.2,0.7]),2),f(oval(0,0.1,0.55,0.8),3),f(oval(-0.18,0.2,0.3,0.5),4),f(disc(0.15,-0.65,0.36),3),f(poly([0.45,-0.72,0.85,-0.55,0.45,-0.42]),2),f(disc(0.22,-0.72,0.09),-2),f(disc(0.24,-0.72,0.04),-1)],
        [f(banana,2),f(disc(-0.85,-0.3,0.07),-1),f(disc(0.85,-0.6,0.07),-1)],
        [f(disc(-0.4,0,0.6),-1),f(disc(-0.35,0.12,0.3),-2),f(beak,2),s(poly([0,-0.2,0.9,0],false),3,0.1),f(disc(-0.3,-0.2,0.12),-2),f(disc(-0.28,-0.2,0.06),-1)],
        [f(petals,3),f(disc(0,0,0.6),3),f(disc(0,0,0.3),2)],
        [f(poly([-0.4,-0.3,-0.3,-0.95,-0.1,-0.45]),0),f(poly([-0.1,-0.4,0,-1.05,0.1,-0.4]),0),f(poly([0.1,-0.45,0.3,-0.95,0.4,-0.3]),0),f(oval(0,0.3,0.5,0.68),2),s(poly([-0.35,0,0.35,0.7,-0.4,0.35,0.4,0.05,-0.1,-0.2,0.1,0.95],false),3,0.06)]
      ];
    },
    streetFood:()=>{
      const noodles=new Path2D(); for(let k=0;k<4;k++){ const y=-0.35+k*0.12; noodles.moveTo(-0.7,y); noodles.quadraticCurveTo(-0.35,y-0.25,0,y); noodles.quadraticCurveTo(0.35,y+0.25,0.7,y); }
      const chilli=new Path2D(); chilli.moveTo(-0.7,-0.7); chilli.quadraticCurveTo(0.6,-0.8,0.9,0.9); chilli.quadraticCurveTo(-0.2,0.5,-0.7,-0.7);
      const steam=new Path2D(); steam.moveTo(-0.15,-0.55); steam.quadraticCurveTo(-0.4,-0.75,-0.15,-0.95); steam.moveTo(0.2,-0.55); steam.quadraticCurveTo(-0.05,-0.75,0.2,-0.95);
      return [
        [f(disc(-0.6,-0.12,0.22),3),f(disc(-0.2,-0.28,0.25),1),f(disc(0.25,-0.25,0.25),3),f(disc(0.65,-0.12,0.2),1),f(arc(0,-0.1,0.95,0,180,true),2),s(arc(0,-0.1,0.7,30,150),0,0.07)],
        [f(oval(0,-0.1,0.8,0.45),2),s(noodles,0,0.05),s(poly([0.2,-0.2,0.8,-0.95],false),4,0.07),s(poly([0.35,-0.2,0.95,-0.8],false),4,0.07),f(arc(0,0,0.9,0,180,true),1)],
        [f(poly([-0.55,-0.4,0.55,-0.4,0.4,0.8,-0.4,0.8]),0),s(arc(0.55,0.15,0.3,270,450),0,0.1),f(oval(0,-0.4,0.55,0.12),4),s(steam,4,0.07)],
        [f(arc(0,0.35,0.9,180,360,true),2),f(box(0,0.4,1.8,0.1,0.05),2),s(poly([-0.4,-0.3,-0.3,-0.05,-0.1,-0.5,-0.05,-0.05,0.15,-0.55,0.2,-0.05,0.4,-0.4,0.45,-0.05],false),4,0.05)],
        [f(chilli,1),f(poly([-0.7,-0.7,-0.95,-0.95,-0.5,-0.85]),3)],
        [f(box(0,0.8,0.18,0.5),4),f(box(0,-0.15,0.8,1.3,0.4),1),s(poly([-0.2,-0.5,-0.2,-0.1],false),-2,0.1)]
      ];
    },
    retroTech:()=>{
      const heart=['-XX-XX-','XXXXXXX','XXXXXXX','-XXXXX-','--XXX--','---X---'], pixels=new Path2D();
      heart.forEach((line,row)=>[...line].forEach((ch,col)=>{ if(ch==='X') pixels.rect(-0.98+col*0.28,-0.84+row*0.28,0.28,0.28); }));
      const corner=new Path2D(); corner.rect(-0.7,-0.7,0.28,0.28);
      return [
        [f(box(0,0,1.7,1.7,0.1),0),f(box(0.15,-0.65,0.9,0.5,0.04),4),f(box(0.35,-0.65,0.16,0.3),-2),f(box(0,0.5,1.2,0.7,0.05),-2),s(poly([-0.4,0.35,0.4,0.35],false),4,0.06),s(poly([-0.4,0.6,0.2,0.6],false),4,0.06)],
        [f(box(0,0,1.9,1.2,0.12),1),f(box(0,-0.15,1.5,0.6,0.06),-2),f(disc(-0.4,-0.15,0.18),4),f(disc(0.4,-0.15,0.18),4),f(box(0,0.5,0.9,0.3,0.1),4)],
        [f(box(0,0.65,1.5,0.5,0.15),3),s(poly([0,0.5,0,-0.3],false),4,0.16),f(disc(0,-0.5,0.38),1),f(disc(-0.5,0.62,0.1),2),f(disc(0.5,0.62,0.1),2)],
        [f(box(0,0.8,1.0,0.2,0.06),0),f(box(0,-0.05,1.8,1.4,0.25),0),f(box(-0.1,-0.05,1.3,1.0,0.2),-1),s(poly([-0.6,-0.25,-0.35,-0.05,-0.6,0.15],false),2,0.07),s(poly([-0.25,0.2,0.05,0.2],false),2,0.07),f(disc(0.75,-0.3,0.1),2),f(disc(0.75,0,0.1),2)],
        [f(pixels,1),f(corner,-2)],
        [f(box(0,0,1.8,0.9,0.4),3),s(poly([-0.6,0,-0.2,0],false),4,0.12),s(poly([-0.4,-0.2,-0.4,0.2],false),4,0.12),f(disc(0.3,0.1,0.12),1),f(disc(0.6,-0.1,0.12),2)]
      ];
    },
    weather:()=>{
      const flake=new Path2D(); for(let k=0;k<6;k++){ const a=k*PI/3,c=Math.cos(a),sn=Math.sin(a); flake.moveTo(0,0); flake.lineTo(0.95*c,0.95*sn); for(const side of [-1,1]){ const b=a+side*PI/5; flake.moveTo(0.6*c,0.6*sn); flake.lineTo(0.6*c+0.3*Math.cos(b),0.6*sn+0.3*Math.sin(b)); } }
      const cloud=new Path2D(); cloud.addPath(disc(-0.5,0.1,0.42)); cloud.addPath(disc(0,-0.2,0.55)); cloud.addPath(disc(0.5,0.1,0.42)); cloud.addPath(roundedRectIn(-0.92,0.1,1.84,0.52,0.26));
      return [
        [f(cloud,0)],
        [f(poly([0.15,-1,-0.6,0.15,-0.1,0.15,-0.3,1,0.6,-0.2,0.05,-0.2]),1)],
        [s(arc(0,0.5,0.9,180,360),3,0.2),s(arc(0,0.5,0.7,180,360),1,0.2),s(arc(0,0.5,0.5,180,360),0,0.2),f(oval(-0.8,0.5,0.2,0.14),-2),f(oval(0.8,0.5,0.2,0.14),-2)],
        [f(arc(0,0,0.95,180,360,true),3),s(poly([0,-0.95,-0.35,0],false),-2,0.06),s(poly([0,-0.95,0.35,0],false),-2,0.06),s(poly([0,0,0,0.7],false),4,0.1),s(arc(-0.2,0.7,0.2,0,180),4,0.1)],
        [s(flake,0,0.12)],
        [f(disc(0,0,0.5),1),s(rays(8,0.7,0.97),1,0.15)]
      ];
    },
    music:()=>{
      const mic=new Path2D(); mic.addPath(disc(0,-0.4,0.4));
      const grid=new Path2D(); grid.moveTo(-0.35,-0.4); grid.lineTo(0.35,-0.4); grid.moveTo(0,-0.78); grid.lineTo(0,-0.02);
      const pick=new Path2D(); pick.moveTo(0,0.95); pick.bezierCurveTo(-0.5,0.55,-0.9,0,-0.85,-0.55); pick.quadraticCurveTo(0,-1,0.85,-0.55); pick.bezierCurveTo(0.9,0,0.5,0.55,0,0.95); pick.closePath();
      return [
        [f(disc(0,0,0.95),4),s(disc(0,0,0.72),1,0.04),s(disc(0,0,0.58),1,0.04),f(disc(0,0,0.35),0),f(disc(0,0,0.06),-3)],
        [s(arc(0,0.05,0.75,180,360),1,0.14),f(box(-0.78,0.25,0.4,0.65,0.15),0),f(box(0.78,0.25,0.4,0.65,0.15),0)],
        [s(poly([-0.22,0.6,-0.22,-0.6],false),1,0.1),s(poly([0.68,0.4,0.68,-0.8],false),1,0.1),f(poly([-0.22,-0.6,0.68,-0.8,0.68,-0.55,-0.22,-0.35]),1),f(disc(-0.45,0.6,0.25),1),f(disc(0.45,0.4,0.25),1)],
        [f(box(0,0,1.2,1.8,0.15),4),f(disc(0,0.35,0.42),0),f(disc(0,0.35,0.15),2),f(disc(0,-0.5,0.2),0)],
        [f(pick,2),s(arc(0,-0.1,0.5,200,280),-2,0.08)],
        [f(poly([-0.18,-0.1,0.18,-0.1,0.1,0.95,-0.1,0.95]),4),f(mic,1),s(grid,-2,0.06)]
      ];
    },
    wonders:()=>{
      const dome=new Path2D(); dome.moveTo(-0.45,0.2); dome.bezierCurveTo(-0.95,-0.2,-0.2,-0.55,0,-0.85); dome.bezierCurveTo(0.2,-0.55,0.95,-0.2,0.45,0.2); dome.closePath();
      const tiers=[]; for(let k=0;k<3;k++){ const yb=0.8-k*0.55,sh=0.2*k; tiers.push(f(box(0,yb-0.1,0.8-0.2*k,0.3),5)); tiers.push(f(poly([-(0.95-sh),yb-0.18,0.95-sh,yb-0.18,0.7-sh,yb-0.42,-(0.7-sh),yb-0.42]),1)); } tiers.push(s(poly([0,-0.75,0,-1.02],false),4,0.08));
      const arch=new Path2D(); arch.moveTo(-0.95,0.9); arch.lineTo(-0.95,-0.1); arcTo(arch,0,-0.1,0.95,180,360); arch.lineTo(0.95,0.9); arch.closePath();
      const opening=new Path2D(); opening.moveTo(-0.45,0.9); opening.lineTo(-0.45,0); arcTo(opening,0,0,0.45,180,360); opening.lineTo(0.45,0.9); opening.closePath();
      const blades=new Path2D(); for(let k=0;k<4;k++){ const a=k*PI/2+0.5; blades.moveTo(0,-0.25); blades.lineTo(0.9*Math.cos(a),-0.25+0.9*Math.sin(a)); }
      return [
        [f(poly([-0.95,0.7,0,-0.8,0.95,0.7]),0),f(poly([0,-0.8,0.95,0.7,0.2,0.7]),5)],
        [f(box(-0.55,0.15,0.16,1.4),1),f(box(0.55,0.15,0.16,1.4),1),f(poly([-1,-0.6,1,-0.6,0.8,-0.38,-0.8,-0.38]),1),f(box(0,-0.15,1.4,0.14),1),f(box(0,-0.64,2,0.1),-1)],
        [f(box(0,0.55,0.6,0.8),0),f(dome,2),s(poly([0,-0.85,0,-1.08],false),1,0.08),f(arc(0,0.9,0.16,180,360,true),4)],
        [f(arch,3),f(opening,-3),f(poly([-0.12,-1.04,0.12,-1.04,0.1,-0.6,-0.1,-0.6]),0)],
        tiers,
        [f(poly([-0.3,0.9,-0.2,-0.1,0.2,-0.1,0.3,0.9]),3),f(poly([-0.25,-0.1,0,-0.38,0.25,-0.1]),1),s(blades,1,0.2),f(disc(0,-0.25,0.1),4)]
      ];
    },
    search:()=>[
      [f(disc(-0.15,-0.15,0.55),-2),s(disc(-0.15,-0.15,0.55),0,0.2),s(poly([0.25,0.25,0.9,0.9],false),1,0.26),s(arc(-0.15,-0.15,0.35,200,260),-2,0.07)],
      [f(box(0.3,-0.3,1.2,1.0,0.12),2),f(box(-0.2,0.15,1.3,1.1,0.12),0),f(box(-0.65,-0.55,0.6,0.25,0.1),0),s(poly([-0.7,0,0.1,0],false),-2,0.09),s(poly([-0.7,0.3,-0.1,0.3],false),-2,0.09)],
      [f(poly([-0.5,-0.9,-0.5,0.7,-0.15,0.35,0.15,0.95,0.4,0.85,0.1,0.25,0.6,0.25]),-1)],
      [f(box(0,0.1,1.7,1.6,0.3),5),f(box(0,-0.05,1.7,1.5,0.3),-2),s(poly([0.25,-0.5,-0.25,0.4],false),-1,0.2)],
      [f(poly([-0.55,-0.95,0.55,-0.95,0.55,0.95,0,0.5,-0.55,0.95]),4),f(star(5,0.25,0.11,0,-0.35),-2)],
      [f(sparkle(-0.1,-0.1,0.9),1),f(sparkle(0.65,0.6,0.35),2)]
    ]
  };
  const MADE = {};
  function paintStickers(g, w, h, t, dark) {
    const pack = LOOKS[t.pack] ? t.pack : 'space';
    const p = pen(dark), look = LOOKS[pack], layers = MADE[pack] || (MADE[pack] = ART[pack]());
    const rng = new Seeded(seed('stickers.' + pack));
    fill(g, rectIn(0,0,w,h), p(look.ground));
    const m = Math.min(w,h), area = (w*h)/(1200*800);
    const count = Math.max(8, Math.min(60, Math.trunc((6 + (t.density===undefined?0.5:t.density)*26) * Math.max(0.5, area))));
    const base = m * (0.04 + 0.075 * (t.scale===undefined?0.5:t.scale));
    const placed = [], first = rng.int(layers.length), px = scaleOf(g);
    const colour = code => code===-1?p([0.20,0.18,0.24]):code===-2?p([1,1,1]):code===-3?p(look.ground):p(look.palette[Math.min(code,look.palette.length-1)]);
    const white = p([1,1,1]);
    for (let i=0;i<count;i++) {
      const r = base * rng.in(0.85, 1.2);
      let spot = null;
      for (let k=0;k<60;k++) {
        const cx = rng.in(0.04, 0.96)*w, cy = rng.in(0.05, 0.95)*h;
        const dx = (cx-w/2)/(w*0.3), dy = (cy-h/2)/(h*0.25);
        if (dx*dx+dy*dy < 1) continue;
        if (placed.some(q => Math.hypot(q.x-cx, q.y-cy) < (q.r+r)*1.35)) continue;
        spot = [cx, cy]; break;
      }
      if (!spot) continue;
      const ls = layers[(i + first) % layers.length], tilt = rng.in(-22, 22), a = tilt*PI/180;
      placed.push({ x: spot[0], y: spot[1], r });
      g.save(); g.translate(spot[0], spot[1]); g.rotate(a); g.scale(r, r);
      g.shadowColor = 'rgb(0 0 0 / ' + (dark ? 0.35 : 0.14) + ')';
      g.shadowBlur = 2 * 0.04 * r * px;
      g.shadowOffsetX = -Math.sin(a) * 0.03 * r * px;
      g.shadowOffsetY = Math.cos(a) * 0.03 * r * px;
      for (const q of ls) { stroke(g, q.path, white, 0.34 + q.width); if (q.width === 0) fill(g, q.path, white); }
      g.shadowColor = 'transparent'; g.shadowBlur = 0; g.shadowOffsetX = 0; g.shadowOffsetY = 0;
      for (const q of ls) { if (q.width === 0) fill(g, q.path, colour(q.colour)); else stroke(g, q.path, colour(q.colour), q.width); }
      g.restore();
    }
  }

  function paint(theme, tuning, g, w, h, dark, shuffle, thumbnail) {
    if (w <= 1 || h <= 1) return;
    let th = theme || 'off', t = Object.assign({ intensity: 1, density: 0.5, scale: 0.5, pack: 'space', solid: '#E8DFD0', gradientA: '#E9B8A0', gradientB: '#8E86B8', angle: 150 }, tuning || {});
    if (shuffle && !thumbnail) {
      const d = dayNumber();
      if (th === 'stickers') t.pack = PACKS[d % PACKS.length][0];
      else if (WALLS.indexOf(th) >= 0) th = WALLS[d % WALLS.length];
    }
    const p = pen(dark), whole = rectIn(0, 0, w, h);
    if (th === 'stickers') { paintStickers(g, w, h, t, dark); }
    else if (th === 'solid') { fill(g, whole, p(channels(t.solid))); }
    else if (th === 'gradient') {
      const a = (t.angle * PI) / 180, dx = Math.cos(a), dy = Math.sin(a), half = (Math.abs(w*dx) + Math.abs(h*dy)) / 2, mx = w/2, my = h/2;
      fill(g, whole, linear(g, mx-dx*half, my-dy*half, mx+dx*half, my+dy*half, [p(channels(t.gradientA)), p(channels(t.gradientB))]));
    }
    else if (WALLS.indexOf(th) >= 0) {
      const sc = new Scene(g, w, h, pen(dark, NIGHT.has(th)), seed(th));
      sc[th](); sc.grain();
    }
    const inten = thumbnail ? 1 : (t.intensity === undefined ? 1 : t.intensity);
    if (inten < 0.999) fill(g, whole, css(channels(dark ? '#1C1C1C' : '#FFFFFF'), 1 - Math.max(0, inten)));
  }

  function renderTo(canvas, theme, tuning, dark, shuffle, thumbnail) {
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const cw = Math.max(1, rect.width || canvas.clientWidth || (thumbnail ? 160 : window.innerWidth));
    const ch = Math.max(1, rect.height || canvas.clientHeight || (thumbnail ? 100 : window.innerHeight));
    const dw = thumbnail ? 480 : Math.ceil(cw / 64) * 64;
    const dh = thumbnail ? Math.round(480 * ch / cw) : Math.ceil(ch / 64) * 64;
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    const pw = Math.max(1, Math.round(dw * (thumbnail ? 1 : dpr))), ph = Math.max(1, Math.round(dh * (thumbnail ? 1 : dpr)));
    canvas.width = pw; canvas.height = ph;
    const g = canvas.getContext('2d');
    g.setTransform(1, 0, 0, 1, 0, 0); g.clearRect(0, 0, pw, ph);
    g.setTransform(pw / dw, 0, 0, ph / dh, 0, 0);
    paint(theme, tuning, g, dw, dh, dark, shuffle, thumbnail);
  }

  window.__searchThemes = { renderTo: renderTo, WALLS: WALLS, PACKS: PACKS };
})();
";
    }
}
