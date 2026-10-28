// Angular UI runs on https://localhost:60726. ApplicationAPI listens on HTTP port 5095.
module.exports = [
  {
    context: ['/api', '/health', '/openapi'],
    target: 'http://localhost:5095',
    changeOrigin: true
  }
];
