// Local, read-only preview. Serves only prototype files and existing sprite assets.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const allowed = ['docs/prototype-v1/', 'src/PokeCorner.App/Data/animated/', 'src/PokeCorner.App/Data/sprites/'];
const types = {'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.png':'image/png','.gif':'image/gif','.svg':'image/svg+xml','.ttf':'font/ttf','.md':'text/plain; charset=utf-8','.json':'application/json'};
http.createServer((req,res) => {
  try {
    if (!['GET','HEAD'].includes(req.method)) { res.writeHead(405); return res.end(); }
    let name = decodeURIComponent(new URL(req.url, 'http://localhost').pathname).replace(/^\/+/, '');
    if (!name) name = 'docs/prototype-v1/index.html';
    if (name.endsWith('/')) name += 'index.html';
    const file = path.resolve(root, name);
    const rel = path.relative(root,file).replaceAll('\\','/');
    if (!allowed.some(prefix=>rel.startsWith(prefix)) || !types[path.extname(file)]) {res.writeHead(403);return res.end();}
    fs.readFile(file,(error,data)=>{
      if(error){res.writeHead(404);return res.end('Not found');}
      res.writeHead(200, {'Content-Type':types[path.extname(file)],'Cache-Control':'no-store'});
      res.end(req.method==='HEAD'?undefined:data);
    });
  } catch { res.writeHead(400);res.end('Bad request'); }
}).listen(Number(process.argv[2] || 4178),'127.0.0.1',()=>console.log(`Preview: http://127.0.0.1:${process.argv[2] || 4178}/docs/prototype-v1/`));
