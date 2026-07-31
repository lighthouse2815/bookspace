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

$health = Invoke-RestMethod -Method Get -Uri "$BaseUrl/health"
if ($health -ne 'Healthy') {
    throw "Health check không hợp lệ: $health"
}

$login = Invoke-BookSpaceRequest `
    -Method Post `
    -Path '/api/auth/login' `
    -Body @{ email = $Email; password = $Password }

if (-not $login.success -or -not $login.data.accessToken) {
    throw 'Đăng nhập smoke test không thành công.'
}

$token = $login.data.accessToken
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
    -not $books.success -or
    -not $dashboard.success -or
    -not $library.success -or
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

$haLinhSearch = @($people.data.items | Where-Object { $_.displayName -eq 'Hà Linh' })
$haLinhSuggestion = @(
    $peopleSuggestions.data.items | Where-Object { $_.displayName -eq 'Hà Linh' }
)
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
$suggestionFields = @($haLinhSuggestion[0].PSObject.Properties.Name)
if (
    $haLinhSearch.Count -ne 1 -or
    @($requiredPeopleFields | Where-Object { $_ -notin $searchFields }).Count -gt 0 -or
    $haLinhSearch[0].reason -ne 'SEARCH_MATCH' -or
    -not $haLinhSearch[0].id -or
    $null -eq $haLinhSearch[0].followerCount -or
    $null -eq $haLinhSearch[0].booksReadCount -or
    ($haLinhSearch[0].PSObject.Properties.Name -contains 'email') -or
    $haLinhSuggestion.Count -ne 1 -or
    @($requiredPeopleFields | Where-Object { $_ -notin $suggestionFields }).Count -gt 0 -or
    $haLinhSuggestion[0].reason -notin $suggestionReasons -or
    $haLinhSuggestion[0].mutualFollowCount -lt 1 -or
    -not $haLinhSuggestion[0].followsYou
) {
    throw 'People Discovery không trả đúng Hà Linh hoặc public DTO/reason mong đợi.'
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
    Books = $books.data.totalItems
    LibraryItems = $library.data.totalItems
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
