# DAOメソッド解説

DAOはSQLiteへSQLを実行し、結果をEntityへ変換します。画面遷移、料金、仮予約上限などの業務判断は行いません。

すべての入力値は`SQLiteParameter`でSQLへ渡します。

### 各DAOの2種類のコンストラクタ

次のDAOには、同じ形のコンストラクタが2種類ずつあります。

- `FacilityDao(SQLiteConnectionFactory connectionFactory)` / `FacilityDao(SQLiteConnection connection, SQLiteTransaction transaction)`
- `RoomDao(SQLiteConnectionFactory connectionFactory)` / `RoomDao(SQLiteConnection connection, SQLiteTransaction transaction)`
- `ReservationDao(SQLiteConnectionFactory connectionFactory)` / `ReservationDao(SQLiteConnection connection, SQLiteTransaction transaction)`
- `ParticipantDao(SQLiteConnectionFactory connectionFactory)` / `ParticipantDao(SQLiteConnection connection, SQLiteTransaction transaction)`

Factory版はDAOが単独で接続を開く場合に使います。ConnectionとTransactionを受け取る版は、`ReservationService`が複数DAOを同じTransactionへ参加させる場合に使います。

## SQLiteConnectionFactory

### `SQLiteConnectionFactory()`

- 引数：なし
- 呼び出し元：Controllerの引数なしコンストラクタ
- 処理：`Web.config`の`ReservationDatabase`接続文字列を直接読みます。設定がない、または空なら`ConfigurationErrorsException`を投げます。

### `SQLiteConnectionFactory(string connectionString)`

- 引数：接続文字列
- 用途：任意のSQLite DBへ接続したい場合
- 処理：空の接続文字列を拒否して保持します。

### `OpenConnection()`

- 戻り値：開かれた`SQLiteConnection`
- 呼び出し元：`ReservationService`、`DaoBase.Execute`
- 処理：Connectionを開き、`PRAGMA foreign_keys = ON;`を実行してから返します。SQLiteでは接続ごとに外部キー制約を有効にする必要があります。

`GetConnectionString()`という補助メソッドは現在ありません。既定コンストラクタが設定取得を直接行います。

## DaoBase

各DAOが継承する共通クラスです。

### `DaoBase(SQLiteConnectionFactory connectionFactory)`

- 用途：DAOが単独でDB操作するとき
- 処理：Factoryを保持し、必要になった時点でConnectionを開けるようにします。

### `DaoBase(SQLiteConnection connection, SQLiteTransaction transaction)`

- 用途：Serviceが複数DAOで同じTransactionを使うとき
- 処理：Connection、Transaction、接続状態を確認して共有用フィールドへ保持します。

### `Execute<T>(Func<SQLiteConnection, SQLiteTransaction, T> operation)`

- 戻り値：渡された処理の結果`T`
- 呼び出し元：各DAOメソッド
- 処理：共有Connectionがあればそれを使います。なければFactoryからConnectionを開きます。
- 補足：DAOには現在も`Func`とラムダ式によるこの共通化があります。ReservationServiceのTransaction処理とは別の仕組みです。

### `CreateCommand(...)`

- 戻り値：SQLとTransactionが設定された`SQLiteCommand`
- 処理：ConnectionからCommandを作り、SQL文字列とTransactionを設定します。

### `AddParameter(...)`

- 処理：Parameter名、`DbType`、値をCommandへ追加します。C#のnullは`DBNull.Value`へ変換します。

### `ToDatabaseDateTime(DateTime value)`

- 戻り値：JSTローカル時刻のISO-8601文字列
- 保存形式：`yyyy-MM-dd'T'HH:mm:ss.fff`
- 処理：UTCまたはPCローカル時刻なら東京時刻へ変換し、`Unspecified`なら入力された時計表示をそのまま使います。

### `FromDatabaseDateTime(string value)`

- 戻り値：`DateTimeKind.Unspecified`のDateTime
- 処理：ミリ秒あり・なしのISO-8601文字列を読みます。対応外の形式なら`FormatException`を投げます。

## FacilityDao

2つのコンストラクタがあります。Factory版は通常の単独読み取り用、ConnectionとTransaction版はServiceから共有する場合に使います。

### `FindAll()`

- 戻り値：全拠点の`List<Facility>`
- 呼び出し元：`ReservationListService.GetFacilities`
- SQL：facilitiesを`facility_id`順でSELECTします。

### `FindById(string facilityId)`

- 戻り値：該当する`Facility`、なければnull
- 呼び出し元：検索Service、一覧Service
- SQL：`facility_id = @facilityId`で1件取得します。

### `MapFacility(SQLiteDataReader reader)`

- 種類：private static補助メソッド
- 処理：DataReaderの1行を`Facility`へ変換します。

## RoomDao

### `FindByFacilityId(string facilityId, int? capacity)`

- 戻り値：条件に合う`List<Room>`
- 呼び出し元：`ReservationSearchService`
- SQL：拠点を必須条件にし、capacityがある場合だけ定員以上の会議室を取得します。

### `FindById(string roomId)`

- 戻り値：該当する`Room`、なければnull
- 呼び出し元：予約Service、一覧Service
- SQL：`room_id = @roomId`で1件取得します。

### `MapRoom(SQLiteDataReader reader)`

- 種類：private static補助メソッド
- 処理：DataReaderの1行を`Room`へ変換します。

## ReservationDao

### `FindById(int reservationId)`

- 戻り値：該当する`Reservation`、なければnull
- 呼び出し元：予約確定、変更、キャンセル
- 補足：Participantsは取得しません。参加者は`ParticipantDao`の責務です。

### `FindByRoomAndDate(string roomId, DateTime date)`

- 戻り値：会議室の指定日に重なる予約一覧
- 呼び出し元：本予約タイムライン検索
- SQL条件：日付範囲と重なる予約を取得し、Cancelledを除外します。

### `FindByUserAndStatus(string userId, ReservationStatus status)`

- 戻り値：ユーザーと状態が一致する予約一覧
- 呼び出し元：`ReservationListService.GetReservationList`

### `CountTemporaryReservations(string userId)`

- 戻り値：ユーザーが持つTentative予約の件数
- 呼び出し元：検索Service、予約Service
- 用途：仮予約3件上限の確認

### `IsRoomAvailable(string roomId, DateTime start, DateTime end, int? excludeReservationId = null)`

- 戻り値：重なる予約が0件ならtrue
- 呼び出し元：検索Service、予約Service
- 重複条件：

```sql
start_datetime < @requestedEnd
AND end_datetime > @requestedStart
AND status <> @cancelledStatus
```

`excludeReservationId`が指定された場合だけ、変更対象自身を除外する条件を追加します。

```sql
AND reservation_id <> @excludeReservationId
```

### `Insert(Reservation reservation)`

- 戻り値：SQLiteが自動採番したReservationId
- 呼び出し元：本予約作成、仮予約作成
- SQL：reservationsへINSERTし、`last_insert_rowid()`を取得します。

### `Update(Reservation reservation)`

- 戻り値：1件以上更新できればtrue
- 呼び出し元：仮予約確定、予約変更
- SQL：ReservationIdが一致する予約の各項目をUPDATEします。

### `UpdateStatus(int reservationId, ReservationStatus status)`

- 戻り値：更新できればtrue
- 呼び出し元：`ReservationService.CancelReservation`
- SQL：Statusだけを更新します。

### `AddReservationParameters(...)`

- 種類：private static補助メソッド
- 処理：INSERTとUPDATEで共通するReservationのParameterをCommandへ追加します。

### `MapReservation(SQLiteDataReader reader)`

- 種類：private static補助メソッド
- 処理：DataReaderの1行を`Reservation`へ変換します。DBの日時TEXTは`FromDatabaseDateTime`でDateTimeへ戻します。

## ParticipantDao

### `FindByReservationId(int reservationId)`

- 戻り値：指定予約の`List<Participant>`
- 呼び出し元：`ReservationListService.GetReservationList`
- SQL：reservation_idが一致する参加者を取得します。

### `InsertAll(int reservationId, List<Participant> participants)`

- 戻り値：登録完了ならtrue
- 呼び出し元：本予約作成、仮予約確定、予約変更
- 処理：参加者を1件ずつパラメータ化したINSERTで登録します。
- Transaction：ServiceからTransactionを渡された場合はそれを使います。単独で呼ばれた場合はParticipantDao自身がローカルTransactionを開始します。

### `DeleteByReservationId(int reservationId)`

- 戻り値：削除件数。0件でもエラーではありません。
- 呼び出し元：仮予約確定、予約変更
- 処理：新しい参加者へ入れ替える前に、指定予約の既存参加者を削除します。

### `InsertParticipants(...)`

- 種類：private static補助メソッド
- 処理：`foreach`で参加者を登録します。リスト内にnullがあれば例外にします。CompanyNameがnullなら空文字を保存します。

## UserDaoについて

`UserDao`は現在のプロジェクトには存在しません。ログイン機能とユーザー検索機能が未実装で、現在の機能から使われていなかったため削除されています。

ただし、将来のログイン機能に備えて、`User` Entity、`UserRole` enum、`users`テーブルは残っています。
