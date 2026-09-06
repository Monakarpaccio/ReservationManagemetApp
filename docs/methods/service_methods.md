# Serviceメソッド解説

ServiceはControllerとDAOの間にあり、予約の業務ルールを判断します。Service内にSQLはありません。

## ReservationSearchService

### `ReservationSearchService(SQLiteConnectionFactory connectionFactory)`

- 引数：SQLite接続を作るFactory
- 戻り値：なし（コンストラクタ）
- 呼び出し元：`ReservationController()`
- 処理：引数がnullでないことを確認し、`FacilityDao`、`RoomDao`、`ReservationDao`を作ります。

### `CanCreateTemporaryReservation(string userId)`

- 引数：ユーザーID
- 戻り値：仮予約が3件未満ならtrue、それ以外はfalse
- 呼び出し元：`ReservationController.TemporarySearch`、`SearchAvailableRoomsForTemporary`
- 次に呼ぶもの：`ReservationDao.CountTemporaryReservations`
- 処理：空のユーザーIDを拒否し、現在のTentative件数を数えます。

### `GetReservationSchedule(string facilityId, DateTime date, int? capacity)`

- 戻り値：`ReservationScheduleViewModel`。条件不正や拠点なしならnull
- 呼び出し元：`ReservationController.Search`
- 次に呼ぶもの：`FacilityDao.FindById`、`RoomDao.FindByFacilityId`、`ReservationDao.FindByRoomAndDate`
- 処理順：
  1. 拠点IDと人数を確認します。
  2. 拠点を取得します。
  3. 画面全体用の`ReservationScheduleViewModel`を作ります。
  4. 条件に合う会議室を取得します。
  5. `foreach`で各会議室の当日予約を取得します。
  6. 会議室1行分の`RoomScheduleViewModel`を追加して返します。

### `SearchAvailableRoomsForTemporary(...)`

```csharp
SearchAvailableRoomsForTemporary(
    string facilityId,
    DateTime date,
    TimeSpan startTime,
    TimeSpan endTime,
    int? capacity,
    string userId)
```

- 戻り値：指定時間に空いている`List<Room>`
- 呼び出し元：`ReservationController.TemporarySearch`
- 次に呼ぶもの：`CanCreateTemporaryReservation`、`RoomDao.FindByFacilityId`、`ReservationDao.IsRoomAvailable`
- 処理順：検索条件、3件上限、営業時間、15分単位を確認します。会議室を1件ずつ空き判定し、空いているものだけを結果へ追加します。

### `IsValidReservationTime(TimeSpan startTime, TimeSpan endTime)`

- 種類：private static補助メソッド
- 戻り値：有効ならtrue
- 確認内容：09:00以降、18:00以前、開始より終了が後、開始・終了が15分単位

### `CombineDateAndTime(DateTime date, TimeSpan time)`

- 種類：private static補助メソッド
- 戻り値：日付と時刻を合わせた`DateTimeKind.Unspecified`の日時
- 理由：DBにはJSTの時計表示をタイムゾーンなしのISO-8601 TEXTで保存するためです。

## ReservationService

DBを変更する予約処理を担当します。次の4メソッドは、各メソッド内で同じConnectionとTransactionをDAOへ渡します。

- `CreateReservation`
- `CreateTemporaryReservation`
- `ConfirmTemporaryReservation`
- `UpdateReservation`

Transactionは共通メソッドへ処理を渡さず、各メソッドの中に`using → BeginTransaction → try → DAO作成 → Commit / Rollback`の順で書かれています。

### `ReservationService(SQLiteConnectionFactory connectionFactory)`

- 引数：SQLite接続を作るFactory
- 呼び出し元：2つのControllerの引数なしコンストラクタ
- 処理：nullを拒否し、後でConnectionを開くためにFactoryを保持します。

### `CreateReservation(ReservationInputViewModel model, string userId)`

- 戻り値：登録成功ならtrue、条件不正ならfalse。DB例外は再スロー
- 呼び出し元：`ReservationController.Create`
- 次に呼ぶDAO：`RoomDao`、`ReservationDao`、`ParticipantDao`

処理順は次のとおりです。

1. `TryGetReservationRange`でViewModel、RoomId、営業時間、15分単位を確認します。
2. userIdとTitleが空でないことを確認します。
3. Connectionを開きます。
4. `BeginTransaction()`でTransactionを開始します。
5. `try`の中で、同じConnectionとTransactionを渡して3つのDAOを作ります。
6. `IsRoomAvailable`で登録直前の空き状況を再確認します。
7. `RoomDao.FindById`でRoomとHourlyRateを取得します。
8. `CalculateTotalPrice`で正式料金を計算します。
9. 参加者リストをコピーし、Confirmedの`Reservation`を作ります。
10. `ReservationDao.Insert`で予約を登録します。
11. `ParticipantDao.InsertAll`で参加者を登録します。
12. 採番されたReservationIdと正式料金をViewModelへ戻します。
13. `Commit()`してtrueを返します。
14. 条件不一致では`Rollback()`してfalseを返します。
15. 例外時は`catch`で`Rollback()`し、`throw`で例外をそのまま上へ返します。

### `CreateTemporaryReservation(...)`

```csharp
CreateTemporaryReservation(
    string roomId,
    DateTime date,
    TimeSpan startTime,
    TimeSpan endTime,
    string userId)
```

- 戻り値：登録成功ならtrue、それ以外はfalse
- 呼び出し元：`ReservationController.CreateTemporary`
- 次に呼ぶDAO：`RoomDao`、`ReservationDao`

処理順：

1. roomId、userId、営業時間、15分単位を確認します。
2. 日付と時刻を開始・終了DateTimeへ合わせます。
3. ConnectionとTransactionを開始します。
4. 同じConnectionとTransactionでDAOを作ります。
5. Tentativeが3件未満か登録直前に再確認します。
6. Roomが存在するか確認します。
7. 指定時間が空いているか再確認します。
8. Title、Remarks、TotalPriceがnullのTentative予約を作ります。
9. `ReservationDao.Insert`で登録します。
10. 成功時はCommitしてtrue、条件不一致はRollbackしてfalse、例外時はRollback後に再スローします。

### `ConfirmTemporaryReservation(int reservationId, ReservationInputViewModel model, string userId)`

- 戻り値：本予約への更新成功ならtrue、それ以外はfalse
- 呼び出し元：`ReservationListController.ConfirmTemporary`
- 次に呼ぶDAO：`RoomDao`、`ReservationDao`、`ParticipantDao`

処理順：

1. model、userId、Titleを確認します。
2. ConnectionとTransactionを開始します。
3. 同じConnectionとTransactionでDAOを作ります。
4. `FindById`で既存予約を取得します。
5. 予約者がuserIdと同じか確認します。
6. StatusがTentativeか確認します。
7. 開始日と終了日が同じか、営業時間内か、15分単位か確認します。
8. 自分の予約IDを除外して空き状況を再確認します。
9. Roomを取得し、正式料金を計算します。
10. Title、Remarks、Status、TotalPrice、Participantsを更新します。
11. `ReservationDao.Update`で既存予約を更新します。
12. 古い参加者を削除し、新しい参加者を登録します。
13. 成功時はCommitします。途中失敗と例外時の扱いは本予約作成と同じです。

新しいReservationはINSERTしません。既存のTentative行をConfirmedへ変えます。

### `UpdateReservation(ReservationInputViewModel model, string userId)`

- 戻り値：変更成功ならtrue、それ以外はfalse
- 呼び出し元：`ReservationListController.Update`
- 次に呼ぶDAO：`RoomDao`、`ReservationDao`、`ParticipantDao`

処理順：

1. 入力日時、ReservationId、userId、Titleを確認します。
2. ConnectionとTransactionを開始します。
3. 既存予約を取得します。
4. 予約者が本人か確認します。
5. StatusがConfirmedか確認します。
6. 自分のReservationIdを除外して、新しいRoomと日時の空きを確認します。
7. RoomとHourlyRateを取得します。
8. Room、日時、Title、Remarks、正式料金、Participantsを新しい値へ変えます。
9. 予約を更新します。
10. 参加者を削除してから登録し直します。
11. 成功時はCommit、条件不一致はRollback、例外時はRollback後に再スローします。

### `CancelReservation(int reservationId, string userId)`

- 戻り値：キャンセル成功ならtrue、それ以外はfalse
- 呼び出し元：`ReservationListController.Cancel`
- 次に呼ぶもの：`ReservationDao.FindById`、`ReservationDao.UpdateStatus`
- 処理：userIdが空でないこと、予約が存在すること、予約者本人であることを確認し、StatusをCancelledへ変更します。Reservation行とParticipantは削除しません。このメソッドは複数テーブル更新を行わないため、明示的なTransactionを開始していません。

### `TryGetReservationRange(...)`

- 種類：private static補助メソッド
- 戻り値：日時を作れればtrue
- 処理：modelとRoomIdを確認し、`IsValidReservationTime`を通過したら日付と開始・終了時刻をDateTimeへまとめます。結果は`out`引数へ入れます。

### `IsValidReservationTime(TimeSpan startTime, TimeSpan endTime)`

- 種類：private static補助メソッド
- 戻り値：営業時間内かつ15分単位ならtrue

### `CombineDateAndTime(DateTime date, TimeSpan time)`

- 種類：private static補助メソッド
- 戻り値：日付と時刻を合わせた`DateTimeKind.Unspecified`の日時

### `CalculateTotalPrice(int hourlyRate, DateTime startDateTime, DateTime endDateTime)`

- 種類：private static補助メソッド
- 戻り値：整数円の正式料金
- 計算：`HourlyRate / 4 × 15分スロット数`
- 補足：`checked`により、計算結果がintの範囲を超えた場合は例外になります。

### `CopyParticipants(IEnumerable<Participant> participants)`

- 種類：private static補助メソッド
- 戻り値：新しい`List<Participant>`
- 処理：nullなら空リスト、それ以外は受け取った参加者を新しいリストへコピーします。

## ReservationListService

### `ReservationListService(SQLiteConnectionFactory connectionFactory)`

- 呼び出し元：`ReservationListController()`
- 処理：Factoryがnullでないことを確認し、Facility、Room、Reservation、ParticipantのDAOを作ります。

### `GetReservationList(string userId, ReservationStatus status)`

- 戻り値：`List<ReservationDetailViewModel>`
- 呼び出し元：一覧Controllerの補助メソッドと詳細検索
- 次に呼ぶもの：`FindByUserAndStatus`、`RoomDao.FindById`、`FacilityDao.FindById`、`ParticipantDao.FindByReservationId`
- 処理：ユーザーと状態で予約を取得し、会議室、拠点、参加者を1件ずつ取得して画面用ViewModelへまとめます。RoomまたはFacilityが見つからない予約は結果へ追加しません。

### `GetFacilities()`

- 戻り値：`List<Facility>`
- 呼び出し元：`ReservationListController.Index`とCancel失敗時
- 次に呼ぶもの：`FacilityDao.FindAll`
- 処理：ホーム画面の拠点プルダウン用一覧を返します。

### `GetCurrentConfirmedReservations(string userId)`

- 戻り値：これから実施する本予約の一覧
- 呼び出し元：`ReservationListController.GetReservationsForList`
- 処理：Confirmedを取得し、`EndDateTime >= DateTime.Now`の予約だけを返します。

### `GetCompletedReservations(string userId)`

- 戻り値：実施済み予約の一覧
- 呼び出し元：`ReservationListController.GetReservationsForList`
- 処理：Confirmedを取得し、`EndDateTime < DateTime.Now`の予約だけを返します。実施済み専用のReservationStatusはありません。
