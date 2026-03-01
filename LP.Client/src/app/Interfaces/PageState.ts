import { ImageData } from './ImageData';
export interface PageState {
  images: ImageData[];  
  filters: {
    ageMin: number;
    ageMax: number;
    selectedCityId: string;
    useGeolocation: boolean;
    radiusKm: number;
    cityControlValue: string | null;
  };
  pagination: {
    currentPage: number;
    hasMore: boolean;
  };
  sortGUID: string;
  scrollPosition: number;
  showFilters: boolean;
  timestamp: number;
  category: string | null;
}
