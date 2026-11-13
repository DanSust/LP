export const environment = {
  production: true,
  apiUrl: 'https://made4love.ru/api',  // HTTPS для продакшена
  journalApiUrl: 'https://made4love.ru/api',
  hubUrl: 'https://made4love.ru/hub',
  useHttps: true,
  apiVersion: 'v1',
  timeout: 10000, // 10 секунд для продакшена
  logLevel: 'error'
};

console.log('!!! PROD ENVIRONMENT LOADED !!!');
