export type PromoCardType = 'bank' | 'ad' | 'info' | 'telegram' | 'premium';
export interface PromoCard {
  id: string;
  type: PromoCardType;
  title: string;
  content: string;
  subtitle?: string;
  icon?: string;
  actionText?: string;
  actionLink?: string;
  bgColor?: string;
}
