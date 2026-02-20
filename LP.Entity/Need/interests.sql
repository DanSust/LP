USE [LP]
GO

-- ОЧИСТКА
DELETE FROM [dbo].[Interests]

-- 🔥 ВАЖНО: Добавляем 'N' перед русским текстом для Unicode
-- Группа 1: Спорт и активный отдых
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'бег', N'/icons/sports/running.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'фитнес', N'/icons/sports/fitness.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'плавание', N'/icons/sports/swimming.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'велоспорт', N'/icons/sports/cycling.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'теннис', N'/icons/sports/tennis.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'йога', N'/icons/sports/yoga.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'футбол', N'/icons/sports/soccer.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'бокс', N'/icons/sports/boxing.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'лыжи', N'/icons/sports/skiing.svg', 1);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'серфинг', N'/icons/sports/surfing.svg', 1);

-- Группа 2: Искусство и культура
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'живопись', N'/icons/art/painting.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'фотография', N'/icons/art/photography.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'театр', N'/icons/art/theater.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'кино', N'/icons/art/cinema.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'музеи', N'/icons/art/museum.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'танцы', N'/icons/art/dance.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'литература', N'/icons/art/literature.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'дизайн', N'/icons/art/design.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'искусство', N'/icons/art/art.svg', 2);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'рукоделие', N'/icons/art/handcraft.svg', 2);

-- Группа 3: Музыка
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'рок', N'/icons/music/rock.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'джаз', N'/icons/music/jazz.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'классика', N'/icons/music/classical.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'электронная музыка', N'/icons/music/electronic.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'гитара', N'/icons/music/guitar.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'вокал', N'/icons/music/singing.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'фестивали', N'/icons/music/festival.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'винил', N'/icons/music/vinyl.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'хип-хоп', N'/icons/music/hiphop.svg', 3);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'поп', N'/icons/music/pop.svg', 3);

-- Группа 4: Еда и кулинария
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'кулинария', N'/icons/food/cooking.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'вино', N'/icons/food/wine.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'рестораны', N'/icons/food/restaurant.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'суши', N'/icons/food/sushi.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'кофе', N'/icons/food/coffee.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'десерты', N'/icons/food/dessert.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'веганство', N'/icons/food/vegan.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'пикник', N'/icons/food/picnic.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'барбекю', N'/icons/food/bbq.svg', 4);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'фермерская еда', N'/icons/food/farm.svg', 4);

-- Группа 5: Путешествия и природа
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'путешествия', N'/icons/travel/travel.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'горы', N'/icons/travel/mountains.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'пляж', N'/icons/travel/beach.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'походы', N'/icons/travel/hiking.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'кемпинг', N'/icons/travel/camping.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'авиация', N'/icons/travel/aviation.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'фото природы', N'/icons/travel/nature-photo.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'велотуры', N'/icons/travel/bike-tour.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'йога на природе', N'/icons/travel/outdoor-yoga.svg', 5);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'океан', N'/icons/travel/ocean.svg', 5);

-- Группа 6: Игры и развлечения
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'игры', N'/icons/gaming/gaming.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'настольные игры', N'/icons/gaming/board-games.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'покер', N'/icons/gaming/poker.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'караоке', N'/icons/entertainment/karaoke.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'концерты', N'/icons/entertainment/concert.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'клубы', N'/icons/entertainment/club.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'стендап', N'/icons/entertainment/standup.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'VR', N'/icons/gaming/vr.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'квесты', N'/icons/gaming/escape.svg', 6);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'косплей', N'/icons/gaming/cosplay.svg', 6);

-- Группа 7: Чтение и обучение
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'чтение', N'/icons/learning/reading.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'психология', N'/icons/learning/psychology.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'языки', N'/icons/learning/languages.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'история', N'/icons/learning/history.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'наука', N'/icons/learning/science.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'философия', N'/icons/learning/philosophy.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'астрология', N'/icons/learning/astrology.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'медитация', N'/icons/learning/meditation.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'курсы', N'/icons/learning/courses.svg', 7);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'технологии', N'/icons/learning/tech.svg', 7);

-- Группа 8: Социальная активность
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'волонтерство', N'/icons/social/volunteering.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'общение', N'/icons/social/socializing.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'семья', N'/icons/social/family.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'друзья', N'/icons/social/friends.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'собаки', N'/icons/social/dogs.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'кошки', N'/icons/social/cats.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'вечеринки', N'/icons/social/party.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'нетворкинг', N'/icons/social/networking.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'благотворительность', N'/icons/social/charity.svg', 8);
INSERT INTO [dbo].[Interests] ([Id], [Name], [Path], [Group]) VALUES (NEWID(), N'наставничество', N'/icons/social/mentorship.svg', 8);

-- Проверяем количество вставленных записей
SELECT COUNT(*) as TotalInterests FROM [dbo].[Interests];
SELECT [Group], COUNT(*) as Count FROM [dbo].[Interests] GROUP BY [Group] ORDER BY [Group];