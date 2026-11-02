/**
 * Генерирует статические SEO-оболочки для публичных маршрутов Angular SPA.
 *
 * Зачем: nginx отдаёт один и тот же index.html на все маршруты, поэтому
 * поисковый робот без выполнения JS видит только <app-root></app-root>.
 * Скрипт создаёт /about/index.html, /events/index.html и 404.html с
 * корректными title, description, canonical, Open Graph и видимым текстом.
 *
 * Запускается после сборки: npm run build (postbuild) или вручную
 * `node scripts/generate-seo-pages.mjs` из каталога LP.Client.
 */
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = join(scriptDir, '..');
const site = 'https://made4love.ru';

const distCandidates = [
  join(projectRoot, 'dist', 'LP.Client', 'browser'),
  join(projectRoot, 'dist', 'LP.Client'),
  join(projectRoot, 'dist')
];

const pages = [
  {
    path: '/about',
    output: 'about/index.html',
    title: 'О сервисе Made4Love — знакомства для серьёзных отношений',
    description:
      'Узнайте, как работает Made4Love: проверка совместимости, живой чат, безопасное общение и сообщество людей, которые ищут серьёзные отношения.',
    robots: 'index,follow',
    h1: 'О сервисе Made4Love',
    paragraphs: [
      'Made4Love — российский сервис знакомств для тех, кто ищет не случайное общение, а серьёзные отношения. Мы создаём пространство, где знакомиться легко и безопасно: анкеты, интересы и ценности помогают сразу понять, есть ли между вами что-то большее.',
      'Сервис работает на основе проверки совместимости в реальном времени: алгоритм сопоставляет интересы, образ жизни и то, как люди общаются. А живой чат на SignalR позволяет поддерживать разговор без задержек — где бы вы ни находились.',
      'Мы верим, что технологии должны помогать чувствам, а не заменять их. Поэтому Made4Love — это не бесконечная лента анкет, а бережное пространство для настоящих встреч.'
    ],
    faq: [
      {
        question: 'Как работает проверка совместимости?',
        answer:
          'Алгоритм сопоставляет интересы, образ жизни и манеру общения. Результат показывается в реальном времени, чтобы вы могли сосредоточиться на людях, с которыми действительно есть общее.'
      },
      {
        question: 'Сервис платный?',
        answer:
          'Создать анкету и начать общение можно бесплатно. Дополнительные возможности и участие в розыгрышах появляются по мере активности в сервисе.'
      },
      {
        question: 'Как получить приз за свадьбу?',
        answer:
          'Найдите пару на Made4Love, сыграйте свадьбу и подтвердите событие. Мы свяжемся с вами и расскажем, как получить денежный приз.'
      },
      {
        question: 'Безопасно ли знакомиться на Made4Love?',
        answer:
          'Мы модерируем анкеты, поддерживаем жалобы и напоминаем о правилах безопасного общения. Не передавайте личные данные и деньги незнакомым людям.'
      }
    ],
    jsonLd: [
      {
        '@context': 'https://schema.org',
        '@type': 'AboutPage',
        name: 'О сервисе Made4Love',
        url: `${site}/about`,
        description:
          'Как работает Made4Love: проверка совместимости, живой чат и безопасное общение для серьёзных отношений.',
        inLanguage: 'ru-RU',
        isPartOf: { '@id': `${site}/#website` }
      },
      {
        '@context': 'https://schema.org',
        '@type': 'FAQPage',
        mainEntity: [
          {
            '@type': 'Question',
            name: 'Как работает проверка совместимости?',
            acceptedAnswer: {
              '@type': 'Answer',
              text: 'Алгоритм сопоставляет интересы, образ жизни и манеру общения. Результат показывается в реальном времени, чтобы вы могли сосредоточиться на людях, с которыми действительно есть общее.'
            }
          },
          {
            '@type': 'Question',
            name: 'Сервис платный?',
            acceptedAnswer: {
              '@type': 'Answer',
              text: 'Создать анкету и начать общение можно бесплатно. Дополнительные возможности и участие в розыгрышах появляются по мере активности в сервисе.'
            }
          },
          {
            '@type': 'Question',
            name: 'Как получить приз за свадьбу?',
            acceptedAnswer: {
              '@type': 'Answer',
              text: 'Найдите пару на Made4Love, сыграйте свадьбу и подтвердите событие. Мы свяжемся с вами и расскажем, как получить денежный приз.'
            }
          },
          {
            '@type': 'Question',
            name: 'Безопасно ли знакомиться на Made4Love?',
            acceptedAnswer: {
              '@type': 'Answer',
              text: 'Мы модерируем анкеты, поддерживаем жалобы и напоминаем о правилах безопасного общения. Не передавайте личные данные и деньги незнакомым людям.'
            }
          }
        ]
      }
    ]
  },
  {
    path: '/events',
    output: 'events/index.html',
    title: 'События и знакомства — Made4Love',
    description:
      'События и встречи Made4Love: узнавайте о новых мероприятиях сервиса и находите людей, с которыми интересно провести время.',
    robots: 'index,follow',
    h1: 'События и знакомства Made4Love',
    paragraphs: [
      'В этом разделе собраны события Made4Love: встречи, розыгрыши и активности для участников сервиса. Следите за обновлениями, чтобы не пропустить новые мероприятия.',
      'События помогают познакомиться в неформальной обстановке и найти людей с близкими интересами. Начните с анкеты, а затем выбирайте событие, которое вам откликается.'
    ],
    jsonLd: [
      {
        '@context': 'https://schema.org',
        '@type': 'CollectionPage',
        name: 'События и знакомства Made4Love',
        url: `${site}/events`,
        description:
          'События и встречи Made4Love: новые мероприятия сервиса и люди с близкими интересами.',
        inLanguage: 'ru-RU',
        isPartOf: { '@id': `${site}/#website` }
      }
    ]
  },
  {
    path: '/404',
    output: '404.html',
    title: 'Страница не найдена — Made4Love',
    description: 'Запрошенная страница не найдена. Вернитесь на главную Made4Love или посмотрите другие разделы сервиса.',
    robots: 'noindex,nofollow',
    canonical: `${site}/404.html`,
    h1: 'Страница не найдена',
    paragraphs: [
      'Возможно, страница была удалена или адрес введён с ошибкой.',
      'Вернитесь на главную или выберите один из разделов ниже.'
    ],
    jsonLd: []
  }
];

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

function replaceOrWarn(html, regex, replacement, label) {
  if (!regex.test(html)) {
    console.warn(`[seo] Не найден тег: ${label}`);
    return html;
  }
  return html.replace(regex, replacement);
}

function renderShell(page) {
  const paragraphs = page.paragraphs
    .map((text) => `      <p>${escapeHtml(text)}</p>`)
    .join('\n');

  const faq = page.faq
    ? `
      <h2>Частые вопросы</h2>
${page.faq
  .map(
    (item) => `      <details>
        <summary>${escapeHtml(item.question)}</summary>
        <p>${escapeHtml(item.answer)}</p>
      </details>`
  )
  .join('\n')}`
    : '';

  return `<div class="seo-shell">
      <h1>${escapeHtml(page.h1)}</h1>
${paragraphs}${faq}
      <nav aria-label="Основные страницы">
        <a href="/">Главная</a>
        <a href="/about">О сервисе</a>
        <a href="/events">События</a>
        <a href="/auth">Создать анкету</a>
        <a href="/assets/terms.html">Условия</a>
        <a href="/assets/privacy.html">Конфиденциальность</a>
      </nav>
    </div>`;
}

function renderPage(template, page) {
  const url = page.canonical || `${site}${page.path}`;
  let html = template;

  html = replaceOrWarn(html, /<title>[\s\S]*?<\/title>/i, `<title>${escapeHtml(page.title)}</title>`, 'title');
  html = replaceOrWarn(
    html,
    /<meta name="description" content="[^"]*">/i,
    `<meta name="description" content="${escapeHtml(page.description)}">`,
    'description'
  );
  html = replaceOrWarn(
    html,
    /<meta name="robots" content="[^"]*">/i,
    `<meta name="robots" content="${page.robots}">`,
    'robots'
  );
  html = replaceOrWarn(
    html,
    /<link rel="canonical" href="[^"]*">/i,
    `<link rel="canonical" href="${escapeHtml(url)}">`,
    'canonical'
  );
  html = replaceOrWarn(
    html,
    /<meta property="og:title" content="[^"]*">/i,
    `<meta property="og:title" content="${escapeHtml(page.title)}">`,
    'og:title'
  );
  html = replaceOrWarn(
    html,
    /<meta property="og:description" content="[^"]*">/i,
    `<meta property="og:description" content="${escapeHtml(page.description)}">`,
    'og:description'
  );
  html = replaceOrWarn(
    html,
    /<meta property="og:url" content="[^"]*">/i,
    `<meta property="og:url" content="${escapeHtml(url)}">`,
    'og:url'
  );
  html = replaceOrWarn(
    html,
    /<meta name="twitter:title" content="[^"]*">/i,
    `<meta name="twitter:title" content="${escapeHtml(page.title)}">`,
    'twitter:title'
  );
  html = replaceOrWarn(
    html,
    /<meta name="twitter:description" content="[^"]*">/i,
    `<meta name="twitter:description" content="${escapeHtml(page.description)}">`,
    'twitter:description'
  );

  if (page.jsonLd && page.jsonLd.length > 0) {
    const blocks = page.jsonLd
      .map((item) => `<script type="application/ld+json">\n${JSON.stringify(item, null, 2)}\n</script>`)
      .join('\n');
    html = html.replace('</head>', `${blocks}\n</head>`);
  }

  html = replaceOrWarn(
    html,
    /<app-root>[\s\S]*?<\/app-root>/,
    `<app-root>\n${renderShell(page)}\n</app-root>`,
    'app-root'
  );

  return html;
}

async function findDistDir() {
  for (const candidate of distCandidates) {
    try {
      await access(join(candidate, 'index.html'));
      return candidate;
    } catch {
      // пробуем следующий вариант
    }
  }
  return null;
}

const distDir = await findDistDir();
if (!distDir) {
  console.error('[seo] Не найден собранный index.html. Сначала выполните сборку Angular.');
  process.exit(1);
}

const template = await readFile(join(distDir, 'index.html'), 'utf8');

for (const page of pages) {
  const html = renderPage(template, page);
  const outputPath = join(distDir, page.output);
  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, html, 'utf8');
  console.log(`[seo] Создано: ${outputPath}`);
}

console.log(`[seo] Готово. Статические страницы записаны в ${distDir}`);
