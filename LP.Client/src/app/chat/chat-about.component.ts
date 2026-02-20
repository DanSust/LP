import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-chat-about',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="about-container">
      <div class="about-content">
        <h1>📋 Правила общения в чате</h1>
        
        <section class="rules-section">
          <h2>Основные принципы</h2>
          <ul class="rules-list">
            <li>🤝 Будьте вежливы и уважайте собеседника</li>
            <li>💬 Пишите понятно и по делу</li>
            <li>⏱️ Отвечайте в разумные сроки</li>
            <li>🔒 Не передавайте личные данные третьим лицам</li>
          </ul>
        </section>

        <section class="rules-section">
          <h2>Запрещено</h2>
          <ul class="rules-list forbidden">
            <li>❌ Оскорбления и угрозы</li>
            <li>❌ Спам и реклама</li>
            <li>❌ Просьбы денег или материальной помощи</li>
            <li>❌ Рассылка непристойного контента</li>
            <li>❌ Пропаганда насилия и нетерпимости</li>
          </ul>
        </section>

        <section class="rules-section">
          <h2>Советы</h2>
          <ul class="rules-list">
            <li>💡 Используйте кнопку "Жалоба" при нарушениях</li>
            <li>💡 Не торопитесь с переходом на другие мессенджеры</li>
            <li>💡 Доверяйте, но проверяйте информацию</li>
          </ul>
        </section>

        <div class="footer-note">
          <p>Нарушение правил может привести к блокировке аккаунта</p>
          <a routerLink="/about">Подробнее о сервисе →</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .about-container {
      --gradient-top: #557A94;
      --gradient-bottom: #7396AE;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100%;
      padding: 20px;
      background: linear-gradient(180deg, var(--gradient-top) 0%, var(--gradient-bottom) 100%);
    }

    .about-content {
      max-width: 600px;
      background: white;
      border-radius: 16px;
      padding: 30px 40px;
      box-shadow: 0 10px 30px rgba(0,0,0,0.2);
    }

    h1 {
      text-align: center;
      color: #667eea;
      margin-bottom: 30px;
    }

    .rules-section {
      margin-bottom: 25px;
    }

    .rules-section h2 {
      color: #333;
      font-size: 18px;
      margin-bottom: 10px;
    }

    .rules-list {
      list-style: none;
      padding: 0;
    }

    .rules-list li {
      padding: 8px 0;
      color: #555;
      line-height: 1.5;
    }

    .rules-list.forbidden li {
      color: #e74c3c;
    }

    .footer-note {
      text-align: center;
      margin-top: 30px;
      padding-top: 20px;
      border-top: 1px solid #eee;
    }

    .footer-note p {
      color: #999;
      font-size: 14px;
    }

    .footer-note a {
      color: #667eea;
      text-decoration: none;
      font-weight: 500;
    }

    .footer-note a:hover {
      text-decoration: underline;
    }

    @media (max-width: 768px) {
      .about-content {
        padding: 20px;
      }
    }
  `]
})
export class ChatAboutComponent { }
