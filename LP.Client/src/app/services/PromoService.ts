import { Injectable } from '@angular/core';
import { PromoCard, PromoCardType } from './../Interfaces/PromoCard';

@Injectable({
  providedIn: 'root'
})
export class PromoCardService {
  private readonly promoCards: PromoCard[] = [
    {
      id: 'promo-bank-1',
      type: 'bank',
      title: 'Поддержите проект',
      subtitle: 'Синхронное Сердцебиение',
      content: 'Сбербанк: 4276 5500 1234 5678\nТинькофф: 5536 9137 8765 4321\nПолучатель: Даниил К.',
      icon: 'favorite',
      bgColor: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)'
    },
    {
      id: 'promo-telegram-1',
      type: 'telegram',
      title: 'Наш Telegram',
      subtitle: 'Советы по отношениям',
      content: 'Присоединяйтесь к каналу, где мы публикуем истории успеха и советы по построению гармоничных отношений.',
      actionText: 'Подписаться',
      actionLink: 'https://t.me/synchroheart',
      icon: 'send',
      bgColor: 'linear-gradient(135deg, #0088cc 0%, #00aced 100%)'
    },
    {
      id: 'promo-premium-1',
      type: 'premium',
      title: 'Synchro Premium',
      subtitle: 'Раскрой все возможности',
      content: 'Неограниченные лайки, приоритет в поиске, видно кто лайкнул вас первым.',
      actionText: 'Узнать больше',
      actionLink: '/premium',
      icon: 'stars',
      bgColor: 'linear-gradient(135deg, #f093fb 0%, #f5576c 100%)'
    },
    {
      id: 'promo-info-1',
      type: 'info',
      title: 'Знаете ли вы?',
      subtitle: 'Факт о синхронности',
      content: 'Пары с синхронным сердцебиением на 40% реже расстаются. Найдите свою гармонию!',
      icon: 'favorite_border',
      bgColor: 'linear-gradient(135deg, #fa709a 0%, #fee140 100%)'
    },
    {
      id: 'promo-ad-1',
      type: 'ad',
      title: 'Flowers & Love',
      subtitle: 'Скидка 20% на первый заказ',
      content: 'Доставка цветов для ваших любимых. Промокод: HEART20',
      actionText: 'Заказать',
      actionLink: 'https://flowers.example.com',
      icon: 'local_florist',
      bgColor: 'linear-gradient(135deg, #a8edea 0%, #fed6e3 100%)'
    }
  ];

  private readonly PROMO_INTERVAL = 12;

  getRandomCard(): PromoCard {
    const randomIndex = Math.floor(Math.random() * this.promoCards.length);
    return {
      ...this.promoCards[randomIndex],
      id: `promo-${Date.now()}-${randomIndex}`
    };
  }

  generateInsertIndices(totalCount: number): Set<number> {
    const indices = new Set<number>();
    for (let i = this.PROMO_INTERVAL; i < totalCount; i += this.PROMO_INTERVAL) {
      const randomOffset = Math.floor(Math.random() * 5) - 2;
      const index = Math.min(Math.max(i + randomOffset, 0), totalCount - 1);
      indices.add(index);
    }
    return indices;
  }

  getAllCards(): PromoCard[] {
    return [...this.promoCards];
  }
}
