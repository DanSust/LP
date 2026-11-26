import { Component, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Router, RouterModule } from "@angular/router";
import { AuthService } from "../services/AuthService";

@Component({
  selector: "about-app",
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: "./about.component.html",
  styleUrls: ["./about.component.scss"]
})
export class AboutComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  readonly currentYear = new Date().getFullYear();

  /** На /about кнопка создания анкеты не нужна авторизованным пользователям. */
  get showRegistrationCta(): boolean {
    return !this.isAboutPage || !this.authService.getCachedUserId();
  }

  /**
   * На главной и на /about используется один компонент, но контент должен
   * отличаться: иначе поисковики видят полный дубль страниц.
   */
  get isAboutPage(): boolean {
    return this.router.url.split(/[?#]/, 1)[0].startsWith("/about");
  }

  features = [
    {
      icon: "🃏",
      title: "Карточки судьбы",
      description: "Интуитивный свайп-интерфейс для просмотра профилей. Листайте фото, изучайте детали от знака зодиака до образа жизни.",
      color: "#FF6B6B"
    },
    {
      icon: "💬",
      title: "Чат с душой",
      description: "Цитирование сообщений, эмодзи-панель и статусы прочтения. Назойливые уведомления отсутствуют — только вы и ваш собеседник.",
      color: "#4ECDC4"
    },
    {
      icon: "⚡",
      title: "Мгновенная связь",
      description: "Технология SignalR обеспечивает доставку сообщений в реальном времени. Никаких задержек между сердцами.",
      color: "#FFE66D"
    }
  ];

  rewards = [
    {
      icon: "☕",
      title: "Еженедельные розыгрыши",
      description: "Выигрывайте фирменные кружки с символикой сервиса. Потому что лучшие разговоры случаются за чашкой кофе."
    },
    {
      icon: "💍",
      title: "Приз за любовь",
      description: "Нашли друг друга и решили сыграть свадьбу? Мы дарим денежный приз на ваше будущее вместе. Ваше счастье — наша награда."
    }
  ];

  stats = [
    { value: "∞", label: "Возможностей", suffix: "" },
    { value: "100", label: "Мгновенная", suffix: "%" },
    { value: "0", label: "Неловких пауз", suffix: "" }
  ];

  scrollToSection(sectionId: string): void {
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }
}
