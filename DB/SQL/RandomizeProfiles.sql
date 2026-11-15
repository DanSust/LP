/*
================================================================================
  RandomizeProfiles.sql
  Заполняет существующие профили случайной информацией.

  Что обновляется:
    - Profiles.Height / Weight          — случайные рост и вес
    - Profiles.Description              — случайное описание из списка
    - Profiles.AgeFrom / AgeTo          — случайный возрастной диапазон
    - Profiles.Aim                      — случайная цель знакомства (0..2)
    - Profiles.CityId                   — случайный город из Cities
    - UserInterests                     — добавляет интересы до 3..6 на профиль
    - Users.Birthday                    — опционально, случайный возраст 18..44

  Настройки:
    @OverwriteExisting = 1  — перезаписать даже заполненные поля
    @OverwriteExisting = 0  — трогать только пустые/дефолтные значения
    @RandomizeBirthday = 1  — также рандомизировать дату рождения
                            (влияет на возраст и знак зодиака в UI)

  Важно:
    - Скрипт меняет только уже существующие Profiles.
    - Фото не добавляются: Photo.Path ссылается на реальный файл.
    - Перед запуском на реальных данных сделайте резервную копию.
================================================================================
*/

USE [LP];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT N'=== Случайное заполнение существующих профилей ===';

DECLARE @OverwriteExisting BIT = 1; -- 1 = перезаписать всё, 0 = только пустые/дефолтные
DECLARE @RandomizeBirthday BIT = 0; -- 1 = случайная дата рождения (18..44 года)

IF NOT EXISTS (SELECT 1 FROM dbo.Profiles)
BEGIN
    PRINT N'Профили не найдены — нечего обновлять.';
    RETURN;
END;

-- ---------------------------------------------------------------------------
-- Справочник описаний
-- ---------------------------------------------------------------------------
DECLARE @Descriptions TABLE
(
    Id   INT IDENTITY(1,1) PRIMARY KEY,
    Text NVARCHAR(500) NOT NULL
);

INSERT INTO @Descriptions (Text) VALUES
(N'Люблю утренние прогулки, хорошую музыку и спонтанные поездки. Ищу человека, с которым можно разделить и тихий вечер дома, и новое приключение.'),
(N'Кофе по утрам, книги по вечерам и длинные разговоры о жизни — примерно так выглядит мой идеальный день.'),
(N'Обожаю горы, походы и запах леса после дождя. В выходные выбираюсь из города, чтобы перезагрузиться.'),
(N'Готовлю вкусный ужин даже по будням, а по выходным пробую новые рестораны. Разделите со мной столик?'),
(N'Верю, что лучшие истории начинаются со случайного знакомства. Давайте проверим?'),
(N'Занимаюсь спортом, слежу за собой и ценю людей, которые делают то же самое.'),
(N'Люблю животных, особенно собак. Если у вас есть питомец — это большой плюс.'),
(N'Фотографирую всё подряд: город, людей, закаты. Иногда получается красиво.'),
(N'Могу бесконечно говорить о кино и сериалах. Устроим марафон?'),
(N'Ценю честность, юмор и умение слушать. Сама стараюсь быть такой же.'),
(N'Люблю путешествия без строгого плана: билет, рюкзак — и вперёд.'),
(N'По вечерам читаю, готовлю или смотрю что-нибудь лёгкое. Иногда выбираюсь на концерты.'),
(N'Спокойная и уютная жизнь мне ближе, чем шумные вечеринки. Но иногда люблю танцевать.'),
(N'Ищу не идеального человека, а своего. С общими интересами и желанием строить что-то настоящее.'),
(N'Люблю море, солнце и долгие прогулки по набережной. Жаль, что в этом городе моря нет.'),
(N'Занимаюсь йогой и медитацией, стараюсь жить осознанно и без лишней суеты.'),
(N'Вкусно готовлю, особенно выпечку. Если это не считается флиртом, то я не знаю, что считается.'),
(N'Могу поддержать разговор почти на любую тему, кроме футбола. Футбол — это отдельная вселенная.'),
(N'Люблю искусство, музеи и неспешные прогулки по городу. Покажите мне свои любимые места?'),
(N'Работаю, учусь новому и стараюсь не забывать про отдых. Ищу человека с похожими ценностями.'),
(N'Обожаю настольные игры и уютные вечера с друзьями. Присоединяйтесь к следующей партии.'),
(N'Люблю ранние подъёмы и тишину утра. Это моё личное время, которое я берегу.'),
(N'Хочу найти человека, с которым можно просто молчать и не чувствовать неловкости.'),
(N'Путешествия, музыка, хорошая еда и искренние разговоры — вот мои главные радости.'),
(N'Не люблю громкие компании, зато обожаю маленькие ужины с близкими людьми.'),
(N'Люблю учиться новому: языки, курсы, книги. Всё, что помогает расти.'),
(N'Моя суперсила — находить уют в мелочах и не проходить мимо интересного.'),
(N'Ищу компанию для совместных вылазок за город, походов и новых впечатлений.'),
(N'Люблю спорт, особенно плавание и велосипед. Активный отдых для меня — лучший.'),
(N'Если вы тоже любите кофе, книги и долгие прогулки — нам стоит познакомиться.');

DECLARE @DescriptionCount INT = (SELECT COUNT(*) FROM @Descriptions);
DECLARE @CityCount INT = (SELECT COUNT(*) FROM dbo.Cities);
DECLARE @CityModulo INT = CASE WHEN @CityCount > 0 THEN @CityCount ELSE 1 END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID('tempdb..#RandomProfile') IS NOT NULL
        DROP TABLE #RandomProfile;

    -- -----------------------------------------------------------------------
    -- Случайные значения на каждый профиль
    -- -----------------------------------------------------------------------
    CREATE TABLE #RandomProfile
    (
        UserId        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Height        INT NOT NULL,
        Weight        INT NOT NULL,
        AgeFrom       INT NOT NULL,
        AgeTo         INT NOT NULL,
        Aim           INT NOT NULL,
        DescriptionId INT NOT NULL,
        CityRn        INT NOT NULL
    );

    INSERT INTO #RandomProfile (UserId, Height, Weight, AgeFrom, AgeTo, Aim, DescriptionId, CityRn)
    SELECT
        p.UserId,
        h.Height,
        h.Height - 100 + r.WeightDelta,
        r.AgeFrom,
        r.AgeFrom + 5 + r.AgeToExtra,
        r.Aim,
        r.DescriptionId,
        r.CityRn
    FROM dbo.Profiles p
    CROSS APPLY
    (
        SELECT 150 + ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 51 AS Height   -- 150..200 см
    ) h
    CROSS APPLY
    (
        SELECT
            -5 + ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 21 AS WeightDelta,      -- вес = рост - 100, +/- правка
            18 + ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 18 AS AgeFrom,          -- 18..35 лет
            ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 16       AS AgeToExtra,      -- +5..20 лет
            ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 3        AS Aim,             -- Aim: 0..2
            1   + ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % @DescriptionCount AS DescriptionId,
            1   + ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % @CityModulo       AS CityRn
    ) r;

    -- -----------------------------------------------------------------------
    -- Описание
    -- -----------------------------------------------------------------------
    UPDATE p
    SET p.Description = d.Text
    FROM dbo.Profiles p
    JOIN #RandomProfile r ON r.UserId = p.UserId
    JOIN @Descriptions d ON d.Id = r.DescriptionId
    WHERE @OverwriteExisting = 1
       OR p.Description IS NULL
       OR LTRIM(RTRIM(p.Description)) = N'';

    PRINT N'Описаний обновлено: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

    -- -----------------------------------------------------------------------
    -- Рост и вес
    -- -----------------------------------------------------------------------
    UPDATE p
    SET p.Height = CASE
                       WHEN @OverwriteExisting = 1 OR p.Height <= 0 OR p.Height = 100 THEN r.Height
                       ELSE p.Height
                   END,
        p.Weight = CASE
                       WHEN @OverwriteExisting = 1 OR p.Weight <= 0 OR p.Weight = 100 THEN r.Weight
                       ELSE p.Weight
                   END
    FROM dbo.Profiles p
    JOIN #RandomProfile r ON r.UserId = p.UserId
    WHERE @OverwriteExisting = 1
       OR p.Height <= 0 OR p.Height = 100
       OR p.Weight <= 0 OR p.Weight = 100;

    PRINT N'Рост/вес обновлены: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

    -- -----------------------------------------------------------------------
    -- Возрастной диапазон
    -- -----------------------------------------------------------------------
    UPDATE p
    SET p.AgeFrom = CASE
                        WHEN @OverwriteExisting = 1 OR p.AgeFrom <= 0 THEN r.AgeFrom
                        ELSE p.AgeFrom
                    END,
        p.AgeTo = CASE
                      WHEN @OverwriteExisting = 1
                           OR p.AgeFrom <= 0
                           OR p.AgeTo <= p.AgeFrom
                           OR p.AgeTo > 80 THEN r.AgeTo
                      ELSE p.AgeTo
                  END
    FROM dbo.Profiles p
    JOIN #RandomProfile r ON r.UserId = p.UserId
    WHERE @OverwriteExisting = 1
       OR p.AgeFrom <= 0
       OR p.AgeTo <= p.AgeFrom
       OR p.AgeTo > 80;

    PRINT N'Возрастной диапазон обновлён: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

    -- -----------------------------------------------------------------------
    -- Цель знакомства (Aim)
    -- -----------------------------------------------------------------------
    UPDATE p
    SET p.Aim = r.Aim
    FROM dbo.Profiles p
    JOIN #RandomProfile r ON r.UserId = p.UserId
    WHERE @OverwriteExisting = 1;

    PRINT N'Aim обновлён: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

    -- -----------------------------------------------------------------------
    -- Город
    -- -----------------------------------------------------------------------
    IF @CityCount > 0
    BEGIN
        ;WITH NumberedCities AS
        (
            SELECT Id, ROW_NUMBER() OVER (ORDER BY NEWID()) AS rn
            FROM dbo.Cities
        )
        UPDATE p
        SET p.CityId = c.Id
        FROM dbo.Profiles p
        JOIN #RandomProfile r ON r.UserId = p.UserId
        JOIN NumberedCities c ON c.rn = r.CityRn
        WHERE @OverwriteExisting = 1
           OR NOT EXISTS (SELECT 1 FROM dbo.Cities c2 WHERE c2.Id = p.CityId);

        PRINT N'Города обновлены: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));
    END
    ELSE
    BEGIN
        PRINT N'Города не найдены — CityId не изменялся.';
    END;

    -- -----------------------------------------------------------------------
    -- Интересы: доводим количество до случайных 3..6 на профиль
    -- -----------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM dbo.Interests)
    BEGIN
        ;WITH Target AS
        (
            SELECT
                p.UserId,
                (SELECT COUNT(*) FROM dbo.UserInterests ui WHERE ui.UserId = p.UserId) AS ExistingCount,
                3 + ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % 4 AS TargetCount
            FROM dbo.Profiles p
        ),
        ToAdd AS
        (
            SELECT UserId, ExistingCount, TargetCount - ExistingCount AS AddCount
            FROM Target
            WHERE TargetCount > ExistingCount
        )
        INSERT INTO dbo.UserInterests (Id, UserId, InterestId, [Order])
        SELECT
            NEWID(),
            a.UserId,
            i.Id,
            a.ExistingCount + ROW_NUMBER() OVER (PARTITION BY a.UserId ORDER BY (SELECT NULL)) - 1
        FROM ToAdd a
        CROSS APPLY
        (
            SELECT TOP (a.AddCount) i2.Id
            FROM dbo.Interests i2
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.UserInterests ui
                WHERE ui.UserId = a.UserId
                  AND ui.InterestId = i2.Id
            )
            ORDER BY NEWID()
        ) i;

        PRINT N'Добавлено интересов: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));
    END
    ELSE
    BEGIN
        PRINT N'Интересы не найдены — UserInterests не изменялись.';
    END;

    -- -----------------------------------------------------------------------
    -- Опционально: случайная дата рождения (возраст 18..44)
    -- -----------------------------------------------------------------------
    IF @RandomizeBirthday = 1
    BEGIN
        UPDATE u
        SET u.Birthday = DATEADD
        (
            DAY,
            -ABS(CAST(CHECKSUM(NEWID()) AS BIGINT)) % (27 * 365),
            DATEADD(YEAR, -18, CAST(GETDATE() AS DATE))
        )
        FROM dbo.Users u
        WHERE EXISTS (SELECT 1 FROM dbo.Profiles p WHERE p.UserId = u.Id);

        PRINT N'Дат рождения обновлено: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));
    END;

    COMMIT TRANSACTION;
    PRINT N'Готово.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT N'Ошибка: ' + ERROR_MESSAGE();
    THROW;
END CATCH;

IF OBJECT_ID('tempdb..#RandomProfile') IS NOT NULL
    DROP TABLE #RandomProfile;

-- ---------------------------------------------------------------------------
-- Итоги
-- ---------------------------------------------------------------------------
SELECT
    COUNT(*) AS Profiles,
    SUM(CASE WHEN Height BETWEEN 150 AND 200 THEN 1 ELSE 0 END) AS WithRandomHeight,
    SUM(CASE WHEN LEN(LTRIM(RTRIM(Description))) > 0 THEN 1 ELSE 0 END) AS WithDescription,
    (SELECT COUNT(*) FROM dbo.UserInterests) AS UserInterests
FROM dbo.Profiles;

-- Пример результата
SELECT TOP (10)
    u.Caption,
    p.Height,
    p.Weight,
    p.AgeFrom,
    p.AgeTo,
    p.Aim,
    c.Name AS City,
    p.Description
FROM dbo.Profiles p
JOIN dbo.Users u ON u.Id = p.UserId
LEFT JOIN dbo.Cities c ON c.Id = p.CityId
ORDER BY NEWID();
