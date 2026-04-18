// Этот файл заменяется на environment.prod.ts при production сборке
export const environment = {
  production: false,
  //apiUrl: 'https://127.0.0.1:7010/api',
  apiUrl: 'https://127.0.0.1/api',
  //apiUrl: '/api',
  //hubUrl: 'https://127.0.0.1:5000',
  hubUrl: 'https://127.0.0.1/hub',
  useHttps: true,
  apiVersion: 'v1',
  timeout: 30000, // 30 секунд для разработки
  logLevel: 'debug'
};

console.log('!!! LOCAL ENVIRONMENT LOADED !!!');
