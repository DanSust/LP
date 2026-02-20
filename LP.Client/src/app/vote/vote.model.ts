export interface Profile {
  id: string;
  name: string;
  description: string;
  age?: number;
  height?: number;
  weight?: number;
  city?: string;
  location?: string;
  appearance?: string;
  lifestyle?: string;
  zodiac?: string;
  photoUrls?: { id: string; path: string }[]; // Массив объектов
  interests?: { id: string; name: string; path: string }[];  // Массив объектов  
}
