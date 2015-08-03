﻿CREATE FUNCTION [dbo].[ParseCSVString]
(
	@CSVString NVARCHAR(MAX)
)
RETURNS @tbl TABLE
(
	[Value] NVARCHAR(255) NOT NULL PRIMARY KEY
)
AS
BEGIN
	DECLARE @i INT
	DECLARE @j INT

	SELECT @i = 1
	WHILE @i <= LEN(@CSVString)
	BEGIN
		SELECT @j = CHARINDEX(',', @CSVString, @i)
		IF @j = 0
			BEGIN
				SELECT @j = LEN(@CSVString) + 1
			END

		INSERT	@tbl
		SELECT	LTRIM(RTRIM(SUBSTRING(@CSVString, @i, @j - @i)))

		SELECT	@i = @j + 1
	END
RETURN
END