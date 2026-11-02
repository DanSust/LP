import { Component } from "@angular/core";
import { RouterLink } from "@angular/router";

@Component({
  selector: "not-found-app",
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="not-found">
      <p class="code">404</p>
      <h1>Страница не найдена</h1>
      <p class="lead">Возможно, страница была удалена или адрес введён с ошибкой.</p>
      <div class="actions">
        <a routerLink="/">На главную</a>
        <a routerLink="/about">О сервисе</a>
        <a routerLink="/events">События</a>
      </div>
    </section>
  `,
  styles: [`
    .not-found {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      padding: 48px 24px;
      color: #fff;
      text-align: center;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    }

    .code {
      margin: 0 0 12px;
      font-size: clamp(64px, 14vw, 140px);
      font-weight: 800;
      line-height: 1;
      color: rgba(255, 255, 255, 0.35);
    }

    h1 {
      margin: 0 0 16px;
      font-size: clamp(24px, 4vw, 40px);
    }

    .lead {
      max-width: 520px;
      margin: 0 0 32px;
      color: rgba(255, 255, 255, 0.75);
      font-size: 18px;
      line-height: 1.6;
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 12px;
    }

    .actions a {
      padding: 12px 24px;
      border: 1px solid rgba(255, 255, 255, 0.35);
      border-radius: 50px;
      background: rgba(255, 255, 255, 0.12);
      color: #fff;
      font-weight: 600;
      text-decoration: none;
      transition: background 0.2s ease, transform 0.2s ease;
    }

    .actions a:hover {
      background: rgba(255, 255, 255, 0.22);
      transform: translateY(-2px);
    }
  `]
})
export class NotFoundComponent { }
