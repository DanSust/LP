export interface City {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
}

export interface NearestCityResponse {
  city: City;
  distance: number;
}
