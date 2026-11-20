export interface ImageData {
  photoId: string | null;
  firstPhotoId?: string | null;
  userId: string;
  name: string;
  age: number;
  interests?: string[];
  category?: string;
  cityId?: string;
  distance?: number | null;
  imageUrl?: string;
  fallbackImageUrl?: string | null;
}
