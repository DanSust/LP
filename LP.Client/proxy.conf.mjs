console.log('>>> PROXY CONFIG LOADED <<<');

export default {
  '/api': {
    target: 'https://127.0.0.1:7010',
    changeOrigin: true,
    secure: false,
    rewrite: (path) => path.replace(/^\/api/, ''),
    configure: (proxy, options) => {
      proxy.on('error', (err, req, res) => {
        console.log('[Proxy Error]', err.message);
      });
      proxy.on('proxyReq', (proxyReq, req, res) => {
        //console.log('[Proxy Req]', req.method, req.url, '->', options.target + proxyReq.path);
      });
      proxy.on('proxyRes', (proxyRes, req, res) => {
        //console.log('[Proxy Res]', proxyRes.statusCode, req.url);
      });
    }
  },
  '/hub': {
    target: 'https://127.0.0.1:5000',
    changeOrigin: true,
    secure: false,
    ws: true,
    rewrite: (path) => path.replace(/^\/hub/, ''),
    configure: (proxy, options) => {
      proxy.on('error', (err, req, res) => {
        console.log('[Proxy Error]', err.message);
      });
      proxy.on('proxyReq', (proxyReq, req, res) => {
        //console.log('[Proxy Req]', req.method, req.url, '->', options.target + proxyReq.path);
      });
      proxy.on('proxyRes', (proxyRes, req, res) => {
        //console.log('[Proxy Res]', proxyRes.statusCode, req.url);
      });
    }
  }
};
