export class InterestDTO {
  id: string = "";
  name: string = "";
  path: string = "";
  group: number = 1;
  selected: boolean = false;
  constructor(
    id: string,
    name: string
  ) { }
}

export class TownDTO {
  id: string = "";
  name: string = "";
  constructor(
    id: string,
    name: string
  ) { }
}

export class QuestionsDTO {
  id: string = "";
  question: string = "";
  order: number = 1;
  constructor(
    id: string,
    name: string
  ) { }
}

export class EventsDTO {  
  createdAt: string = "";
  title: string = "";
  description: string = "";
  isNew: number = 0;
  constructor(
    createdAt: string    
  ) { }
}
