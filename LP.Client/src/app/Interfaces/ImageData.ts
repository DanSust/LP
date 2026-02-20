export interface ImageData {
  photoId: string;
  userId: string;
  name: string;
  age: number;
  interests: string[];
  category: string;
  cityId?: string;
  distance?: number;
  imageUrl?: string;
}
