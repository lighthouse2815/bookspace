param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$Email = 'reader@bookspace.local',
    [string]$Password = 'Reader123!'
)

$ErrorActionPreference = 'Stop'

function Invoke-BookSpaceRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Method,
        [Parameter(Mandatory)]
        [string]$Path,
        [object]$Body,
        [string]$AccessToken
    )

    $parameters = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        ContentType = 'application/json'
    }

    if ($null -ne $Body) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
    }

    if ($AccessToken) {
        $parameters.Headers = @{ Authorization = "Bearer $AccessToken" }
    }

    Invoke-RestMethod @parameters
}

function Invoke-BookSpaceExpectedError {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [string]$AccessToken
    )

    try {
        $payload = Invoke-BookSpaceRequest `
            -Method Get `
            -Path $Path `
            -AccessToken $AccessToken

        return [pscustomobject]@{
            StatusCode = 200
            Payload = $payload
        }
    }
    catch {
        $errorRecord = $_
        $response = $errorRecord.Exception.Response
        $statusCode = $null
        $rawBody = [string]$errorRecord.ErrorDetails.Message

        if ($null -ne $response -and $response.PSObject.Properties.Name -contains 'StatusCode') {
            $statusCode = [int]$response.StatusCode
        }

        if (
            [string]::IsNullOrWhiteSpace($rawBody) -and
            $null -ne $response -and
            $response.PSObject.Methods.Name -contains 'GetResponseStream'
        ) {
            $stream = $response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                try {
                    $rawBody = $reader.ReadToEnd()
                }
                finally {
                    $reader.Dispose()
                }
            }
        }

        $payload = $null
        if (-not [string]::IsNullOrWhiteSpace($rawBody)) {
            try {
                $payload = $rawBody | ConvertFrom-Json
            }
            catch {
                $payload = $null
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Payload = $payload
        }
    }
}

$health = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health"
if ($health -ne 'Healthy') {
    throw "Health check không hợp lệ: $health"
}

$unauthorizedRecommendations = Invoke-BookSpaceExpectedError `
    -Path '/api/books/recommendations?page=1&pageSize=12'

$login = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/login' `
    -Body @{ email = $Email; password = $Password }

if (-not $login.success -or -not $login.data.accessToken) {
    throw 'Đăng nhập smoke test không thành công.'
}

$token = $login.data.accessToken
$recommendations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $token
$recommendationsRepeat = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $token
$feed = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/feed?type=READING&page=1&pageSize=10' `
    -AccessToken $token
$invalidFeedType = Invoke-BookSpaceExpectedError `
    -Path '/api/feed?type=UNKNOWN&page=1&pageSize=10' `
    -AccessToken $token
$people = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users?search=H%C3%A0%20Linh&page=1&pageSize=20' `
    -AccessToken $token
$peopleSuggestions = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/users/suggestions?page=1&pageSize=20' `
    -AccessToken $token
$books = Invoke-BookSpaceRequest -Method Get -Path '/api/books?page=1&pageSize=8' -AccessToken $token
$dashboard = Invoke-BookSpaceRequest -Method Get -Path '/api/dashboard' -AccessToken $token
$library = Invoke-BookSpaceRequest -Method Get -Path '/api/library?page=1&pageSize=20' -AccessToken $token
$adminLogin = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/login' `
    -Body @{ email = 'admin@bookspace.local'; password = 'Admin123!' }
if (-not $adminLogin.success -or -not $adminLogin.data.accessToken) {
    throw 'Đăng nhập admin cho cold-start recommendation không thành công.'
}
$adminToken = $adminLogin.data.accessToken
$adminLibrary = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/library?page=1&pageSize=20' `
    -AccessToken $adminToken
$adminRecommendations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $adminToken
$adminRecommendationsRepeat = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/books/recommendations?page=1&pageSize=12' `
    -AccessToken $adminToken
$readingSessions = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/reading-sessions?page=1&pageSize=20' `
    -AccessToken $token
$activeReadingSession = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/reading-sessions/active' `
    -AccessToken $token
$goals = Invoke-BookSpaceRequest -Method Get -Path '/api/reading-goals?page=1&pageSize=20' -AccessToken $token
$notes = Invoke-BookSpaceRequest -Method Get -Path '/api/reading-notes?page=1&pageSize=20' -AccessToken $token
$clubs = Invoke-BookSpaceRequest -Method Get -Path '/api/clubs?page=1&pageSize=20' -AccessToken $token
$clubInvitations = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/clubs/invitations?page=1&pageSize=20' `
    -AccessToken $token
$clubDetail = $null
$clubMembers = $null
$clubSprints = $null
$sprintDetail = $null
$sprintLeaderboard = $null
$sprintTimeline = $null
if ($clubs.success -and $clubs.data.items.Count -gt 0) {
    $clubId = $clubs.data.items[0].id
    $clubDetail = Invoke-BookSpaceRequest -Method Get -Path "/api/clubs/$clubId" -AccessToken $token
    $clubMembers = Invoke-BookSpaceRequest `
        -Method Get `
        -Path "/api/clubs/$clubId/members?page=1&pageSize=20" `
        -AccessToken $token
    $clubSprints = Invoke-BookSpaceRequest `
        -Method Get `
        -Path "/api/clubs/$clubId/reading-sprints?page=1&pageSize=20" `
        -AccessToken $token
    if ($clubSprints.success -and $clubSprints.data.items.Count -gt 0) {
        $sprintId = $clubSprints.data.items[0].id
        $sprintDetail = Invoke-BookSpaceRequest `
            -Method Get `
            -Path "/api/clubs/$clubId/reading-sprints/$sprintId" `
            -AccessToken $token
        $sprintLeaderboard = Invoke-BookSpaceRequest `
            -Method Get `
            -Path "/api/clubs/$clubId/reading-sprints/$sprintId/leaderboard?page=1&pageSize=20" `
            -AccessToken $token
        $sprintTimeline = Invoke-BookSpaceRequest `
            -Method Get `
            -Path "/api/clubs/$clubId/reading-sprints/$sprintId/timeline?page=1&pageSize=20" `
            -AccessToken $token
    }
}
$insightsOverview = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/overview?days=30&utcOffsetMinutes=420' `
    -AccessToken $token
$insightsCalendar = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/calendar?days=365&utcOffsetMinutes=420' `
    -AccessToken $token
$insightsWeekly = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/weekly?weeks=12&utcOffsetMinutes=420' `
    -AccessToken $token
$insightsMonthly = Invoke-BookSpaceRequest `
    -Method Get `
    -Path '/api/insights/monthly?months=12&utcOffsetMinutes=420' `
    -AccessToken $token

if (
    -not $people.success -or
    -not $peopleSuggestions.success -or
    -not $recommendations.success -or
    -not $recommendationsRepeat.success -or
    -not $adminLibrary.success -or
    -not $adminRecommendations.success -or
    -not $adminRecommendationsRepeat.success -or
    -not $feed.success -or
    -not $books.success -or
    -not $dashboard.success -or
    -not $library.success -or
    -not $readingSessions.success -or
    -not $activeReadingSession.success -or
    -not $goals.success -or
    -not $notes.success -or
    -not $clubs.success -or
    -not $clubInvitations.success -or
    ($null -ne $clubDetail -and -not $clubDetail.success) -or
    ($null -ne $clubMembers -and -not $clubMembers.success) -or
    ($null -ne $clubSprints -and -not $clubSprints.success) -or
    ($null -ne $sprintDetail -and -not $sprintDetail.success) -or
    ($null -ne $sprintLeaderboard -and -not $sprintLeaderboard.success) -or
    ($null -ne $sprintTimeline -and -not $sprintTimeline.success) -or
    -not $insightsOverview.success -or
    -not $insightsCalendar.success -or
    -not $insightsWeekly.success -or
    -not $insightsMonthly.success
) {
    throw 'Một API lõi trả về envelope thất bại.'
}

if (
    $unauthorizedRecommendations.StatusCode -ne 401 -or
    $null -eq $unauthorizedRecommendations.Payload -or
    $unauthorizedRecommendations.Payload.success -ne $false -or
    $unauthorizedRecommendations.Payload.code -ne 'UNAUTHORIZED' -or
    $unauthorizedRecommendations.Payload.message -ne 'Bạn cần đăng nhập để tiếp tục.'
) {
    throw 'Recommendation không trả đúng envelope 401 UNAUTHORIZED tiếng Việt.'
}

$recommendationItems = @($recommendations.data.items)
$recommendationItemsRepeat = @($recommendationsRepeat.data.items)
$recommendationPageFields = @($recommendations.data.PSObject.Properties.Name)
$requiredRecommendationPageFields = @('items', 'page', 'pageSize', 'totalItems', 'totalPages')
$requiredRecommendationItemFields = @('book', 'reasonCode', 'reasonText')
$requiredRecommendationBookFields = @('id', 'title', 'shelf')
$recommendationReasonTexts = @{
    FOLLOWED_READER_LIKED = 'Được độc giả bạn theo dõi đánh giá cao.'
    MATCHED_AUTHOR = 'Cùng tác giả với sách bạn quan tâm.'
    MATCHED_CATEGORY = 'Cùng thể loại với sách bạn quan tâm.'
    POPULAR_FALLBACK = 'Được cộng đồng BookSpace đánh giá cao.'
}
$ownedBookIds = @($library.data.items | ForEach-Object { [string]$_.bookId })
$recommendationSmokeInvalid = (
    @(
        $requiredRecommendationPageFields |
            Where-Object { $_ -notin $recommendationPageFields }
    ).Count -gt 0 -or
    $recommendations.data.page -ne 1 -or
    $recommendations.data.pageSize -ne 12 -or
    $recommendations.data.totalItems -lt $recommendationItems.Count -or
    $recommendationItems.Count -eq 0
)

foreach ($recommendationItem in $recommendationItems) {
    $itemFields = @($recommendationItem.PSObject.Properties.Name)
    $bookFields = @($recommendationItem.book.PSObject.Properties.Name)
    $expectedReasonText = $recommendationReasonTexts[[string]$recommendationItem.reasonCode]
    if (
        @($requiredRecommendationItemFields | Where-Object { $_ -notin $itemFields }).Count -gt 0 -or
        @($itemFields | Where-Object { $_ -notin $requiredRecommendationItemFields }).Count -gt 0 -or
        @($requiredRecommendationBookFields | Where-Object { $_ -notin $bookFields }).Count -gt 0 -or
        -not $recommendationItem.book.id -or
        -not $recommendationItem.book.title -or
        $null -ne $recommendationItem.book.shelf -or
        [string]::IsNullOrWhiteSpace([string]$expectedReasonText) -or
        $recommendationItem.reasonText -ne $expectedReasonText -or
        [string]$recommendationItem.book.id -in $ownedBookIds
    ) {
        $recommendationSmokeInvalid = $true
        break
    }
}

$recommendationSignature = @(
    $recommendationItems |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
$recommendationRepeatSignature = @(
    $recommendationItemsRepeat |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
if (
    $recommendationItemsRepeat.Count -ne $recommendationItems.Count -or
    $recommendationRepeatSignature -ne $recommendationSignature
) {
    $recommendationSmokeInvalid = $true
}

if ($recommendationSmokeInvalid) {
    throw 'Recommendation PageResult, reason mapping, own-library exclusion hoặc ordering không hợp lệ.'
}

$adminRecommendationItems = @($adminRecommendations.data.items)
$adminRecommendationItemsRepeat = @($adminRecommendationsRepeat.data.items)
$adminRecommendationSignature = @(
    $adminRecommendationItems |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
$adminRecommendationRepeatSignature = @(
    $adminRecommendationItemsRepeat |
        ForEach-Object { '{0}:{1}' -f $_.book.id, $_.reasonCode }
) -join '|'
if (
    $adminLibrary.data.totalItems -ne 0 -or
    $adminRecommendationItems.Count -eq 0 -or
    $adminRecommendations.data.page -ne 1 -or
    $adminRecommendations.data.pageSize -ne 12 -or
    @($adminRecommendationItems | Where-Object { $_.reasonCode -ne 'POPULAR_FALLBACK' }).Count -gt 0 -or
    $adminRecommendationItemsRepeat.Count -ne $adminRecommendationItems.Count -or
    $adminRecommendationRepeatSignature -ne $adminRecommendationSignature
) {
    throw 'Cold-start recommendation fallback hoặc ordering không hợp lệ.'
}

$haLinhSearch = @($people.data.items | Where-Object { $_.displayName -eq 'Hà Linh' })
$suggestionItems = @($peopleSuggestions.data.items)
$suggestionReasons = @(
    'MUTUAL_FOLLOWS',
    'FOLLOWS_YOU',
    'POPULAR_READER',
    'ACTIVE_READER',
    'NEW_READER'
)
$requiredPeopleFields = @(
    'id',
    'displayName',
    'bio',
    'avatarUrl',
    'followerCount',
    'booksReadCount',
    'isFollowing',
    'followsYou',
    'mutualFollowCount',
    'reason',
    'reasonText'
)
$searchFields = @($haLinhSearch[0].PSObject.Properties.Name)
$suggestionPageFields = @($peopleSuggestions.data.PSObject.Properties.Name)
$requiredSuggestionPageFields = @('items', 'page', 'pageSize', 'totalItems', 'totalPages')
$peopleSmokeInvalid = (
    $haLinhSearch.Count -ne 1 -or
    @($requiredPeopleFields | Where-Object { $_ -notin $searchFields }).Count -gt 0 -or
    $haLinhSearch[0].reason -ne 'SEARCH_MATCH' -or
    -not $haLinhSearch[0].id -or
    $null -eq $haLinhSearch[0].followerCount -or
    $null -eq $haLinhSearch[0].booksReadCount -or
    ($haLinhSearch[0].PSObject.Properties.Name -contains 'email') -or
    @(
        $requiredSuggestionPageFields |
            Where-Object { $_ -notin $suggestionPageFields }
    ).Count -gt 0 -or
    $peopleSuggestions.data.totalItems -lt $suggestionItems.Count
)
foreach ($suggestionItem in $suggestionItems) {
    $suggestionFields = @($suggestionItem.PSObject.Properties.Name)
    if (
        @($requiredPeopleFields | Where-Object { $_ -notin $suggestionFields }).Count -gt 0 -or
        $suggestionItem.reason -notin $suggestionReasons -or
        -not $suggestionItem.reasonText -or
        ($suggestionFields -contains 'email')
    ) {
        $peopleSmokeInvalid = $true
        break
    }
}
if ($peopleSmokeInvalid) {
    throw 'People Discovery public DTO, PageResult hoặc reason không hợp lệ.'
}

$feedItems = @($feed.data.items)
$feedTypes = @($feedItems | ForEach-Object { $_.type })
$feedPageFields = @($feed.data.PSObject.Properties.Name)
$requiredFeedPageFields = @('items', 'page', 'pageSize', 'totalItems', 'totalPages')
$feedSmokeInvalid = (
    @($requiredFeedPageFields | Where-Object { $_ -notin $feedPageFields }).Count -gt 0 -or
    $feed.data.page -ne 1 -or
    $feed.data.pageSize -ne 10 -or
    $feed.data.totalItems -lt $feedItems.Count -or
    $feedItems.Count -eq 0 -or
    'READING_PROGRESS' -notin $feedTypes -or
    'BOOK_FINISHED' -notin $feedTypes
)

foreach ($feedItem in $feedItems) {
    $feedItemFields = @($feedItem.PSObject.Properties.Name)
    if (
        $feedItem.type -notin @('READING_PROGRESS', 'BOOK_FINISHED') -or
        -not $feedItem.id -or
        -not $feedItem.createdAt -or
        $feedItemFields -contains 'note'
    ) {
        $feedSmokeInvalid = $true
        break
    }
}

for ($index = 1; $index -lt $feedItems.Count; $index++) {
    $previousItem = $feedItems[$index - 1]
    $currentItem = $feedItems[$index]
    $previousCreatedAt = [DateTimeOffset]::Parse([string]$previousItem.createdAt)
    $currentCreatedAt = [DateTimeOffset]::Parse([string]$currentItem.createdAt)

    if (
        $currentCreatedAt -gt $previousCreatedAt -or
        (
            $currentCreatedAt -eq $previousCreatedAt -and
            [string]::CompareOrdinal([string]$currentItem.id, [string]$previousItem.id) -gt 0
        )
    ) {
        $feedSmokeInvalid = $true
        break
    }
}

$serializedFeed = $feed.data | ConvertTo-Json -Depth 8 -Compress
foreach ($readingSession in @($readingSessions.data.items)) {
    if (
        -not [string]::IsNullOrWhiteSpace([string]$readingSession.note) -and
        $serializedFeed.Contains([string]$readingSession.note)
    ) {
        $feedSmokeInvalid = $true
        break
    }
}

if ($feedSmokeInvalid) {
    throw 'Feed filter, paging, ordering or note isolation is invalid.'
}

if ($null -ne $activeReadingSession.data) {
    $activeReadingFields = @($activeReadingSession.data.PSObject.Properties.Name)
    $requiredActiveReadingFields = @(
        'id',
        'bookId',
        'book',
        'status',
        'startPage',
        'startedAt',
        'elapsedSeconds',
        'updatedAt'
    )
    if (
        @($requiredActiveReadingFields | Where-Object { $_ -notin $activeReadingFields }).Count -gt 0 -or
        $activeReadingSession.data.status -notin @('RUNNING', 'PAUSED') -or
        [long]$activeReadingSession.data.elapsedSeconds -lt 0 -or
        $activeReadingFields -contains 'note' -or
        $activeReadingFields -contains 'userId' -or
        $activeReadingFields -contains 'accumulatedSeconds'
    ) {
        throw 'Focus Reading active DTO hoặc privacy contract không hợp lệ.'
    }
}

if (
    $invalidFeedType.StatusCode -ne 400 -or
    $null -eq $invalidFeedType.Payload -or
    $invalidFeedType.Payload.success -ne $false -or
    $invalidFeedType.Payload.code -ne 'INVALID_FEED_TYPE' -or
    [string]::IsNullOrWhiteSpace([string]$invalidFeedType.Payload.message) -or
    -not [regex]::IsMatch([string]$invalidFeedType.Payload.message, '[^\u0000-\u007F]')
) {
    throw 'Invalid feed type did not return the localized 400 contract.'
}

if (
    $insightsCalendar.data.daysData.Count -ne 365 -or
    $insightsWeekly.data.items.Count -ne 12 -or
    $insightsMonthly.data.items.Count -ne 12
) {
    throw "Reading Insights không trả đủ các bucket lịch, tuần hoặc tháng."
}

[pscustomobject]@{
    Health = $health
    User = $login.data.user.email
    PeopleSearchResults = $people.data.totalItems
    PeopleSuggestions = $peopleSuggestions.data.totalItems
    Recommendations = $recommendations.data.totalItems
    ColdStartRecommendations = $adminRecommendations.data.totalItems
    ReadingFeedItems = $feed.data.totalItems
    Books = $books.data.totalItems
    LibraryItems = $library.data.totalItems
    ReadingSessions = $readingSessions.data.totalItems
    FocusSessionState = if ($null -ne $activeReadingSession.data) { $activeReadingSession.data.status } else { 'NONE' }
    BooksRead = $dashboard.data.booksRead
    ReadingGoals = $goals.data.totalItems
    ReadingNotes = $notes.data.totalItems
    Clubs = $clubs.data.totalItems
    ClubInvitations = $clubInvitations.data.totalItems
    FirstClubMembers = if ($null -ne $clubMembers) { $clubMembers.data.totalItems } else { 0 }
    FirstClubReadingSprints = if ($null -ne $clubSprints) { $clubSprints.data.totalItems } else { 0 }
    FirstSprintParticipants = if ($null -ne $sprintLeaderboard) { $sprintLeaderboard.data.totalItems } else { 0 }
    FirstSprintTimelineItems = if ($null -ne $sprintTimeline) { $sprintTimeline.data.totalItems } else { 0 }
    CurrentStreak = $insightsOverview.data.currentStreak
    InsightCalendarDays = $insightsCalendar.data.daysData.Count
    InsightWeeks = $insightsWeekly.data.items.Count
    InsightMonths = $insightsMonthly.data.items.Count
    Status = 'PASS'
}
