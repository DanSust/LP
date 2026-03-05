export const environment = {
  production: true,
  apiUrl: 'https://made4love.ru',  // HTTPS для продакшена
  useHttps: true,
  apiVersion: 'v1',
  timeout: 10000, // 10 секунд для продакшена
  logLevel: 'error'
};

console.log('!!! PROD ENVIRONMENT LOADED !!!');
