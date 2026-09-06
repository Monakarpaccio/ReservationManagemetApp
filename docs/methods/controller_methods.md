# Controllerメソッド解説

この資料は、現在のControllerを上から順番に追うための補助資料です。

Controllerの仕事は、画面から値を受け取り、Serviceを呼び、次のViewまたはリダイレクト先を返すことです。SQL、料金計算、重複判定はControllerに書きません。

現在はログイン未実装のため、どちらのControllerも次の定数を使います。

```csharp
private const string CurrentUserId = "U001";
```

将来は、この定数をSessionなどからログインユーザーIDを取得する処理へ置き換えます。

## ReservationController

新しい本予約・仮予約を作る流れを担当します。

### `ReservationController()`

- 引数：なし
- 戻り値：なし（コンストラクタ）
- 呼び出し元：ASP.NET MVC
- 次に作るもの：`SQLiteConnectionFactory`、`ReservationSearchService`、`ReservationService`
- 処理：2つのServiceを直接生成します。Serviceを引数で受け取る別コンストラクタや、Service生成専用メソッドはありません。

### `Search(string facilityId, DateTime? date, int? capacity)`

- HTTP：GET
- 引数：拠点ID、日付、任意の人数
- 戻り値：`Search.cshtml`と`ReservationScheduleViewModel`
- 呼び出し元：ホームの新規本予約検索フォーム
- 次に呼ぶもの：`ReservationSearchService.GetReservationSchedule`
- 処理順：日付があるか確認し、Serviceから拠点内の会議室と当日の予約を取得し、タイムライン画面へ渡します。取得できない場合は`ModelState`へエラーを追加します。

### `TemporarySearch(...)`

```csharp
TemporarySearch(
    string facilityId,
    DateTime? date,
    TimeSpan? startTime,
    TimeSpan? endTime,
    int? capacity)
```

- HTTP：GET
- 戻り値：`TemporarySearch.cshtml`と`List<Room>`
- 呼び出し元：ホームの新規仮予約検索フォーム
- 次に呼ぶもの：`CanCreateTemporaryReservation`、`SearchAvailableRoomsForTemporary`
- 処理順：検索条件を`ViewData`へ保存し、必須日時と仮予約3件上限を確認してから、空いている会議室を取得します。

### `Input(...)`

```csharp
Input(
    string roomId,
    string roomName,
    string facilityName,
    DateTime date,
    TimeSpan startTime,
    TimeSpan endTime)
```

- HTTP：GET
- 戻り値：`Input.cshtml`と`ReservationInputViewModel`
- 呼び出し元：本予約タイムラインの「次へ」
- 次に呼ぶもの：Serviceは呼ばない
- 処理：タイムラインから受け取った値を入力用ViewModelへ詰め替えます。この時点ではDBへ登録しません。

### `Confirm(ReservationInputViewModel model)`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：正常時は`Confirm.cshtml`、入力エラー時は`Input.cshtml`
- 呼び出し元：新規本予約入力画面
- 次に呼ぶもの：Serviceは呼ばない
- 処理：`ModelState.IsValid`を確認し、入力内容を確認画面へ渡します。DB更新はありません。

### `Create(ReservationInputViewModel model)`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：成功時は`ReservationList/Index`へリダイレクト、失敗時は`Confirm.cshtml`
- 呼び出し元：新規本予約確認画面
- 次に呼ぶもの：`ReservationService.CreateReservation`
- 処理：入力検証後にServiceへ登録を依頼します。正式料金と重複判定はServiceが行います。

### `CreateTemporary(...)`

```csharp
CreateTemporary(
    string roomId,
    DateTime date,
    TimeSpan startTime,
    TimeSpan endTime)
```

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：成功時は`ReservationList/Index`へリダイレクト、失敗時は`TemporarySearch.cshtml`
- 呼び出し元：仮予約検索結果
- 次に呼ぶもの：`ReservationService.CreateTemporaryReservation`
- 処理：選択された会議室と日時をServiceへ渡します。上限、営業時間、空き状況はServiceが登録直前に再確認します。

## ReservationListController

ホーム表示と、すでに存在する予約の操作を担当します。

### `ReservationListController()`

- 引数：なし
- 戻り値：なし（コンストラクタ）
- 呼び出し元：ASP.NET MVC
- 次に作るもの：`SQLiteConnectionFactory`、`ReservationListService`、`ReservationService`
- 処理：2つのServiceを直接生成します。Serviceを受け取る別コンストラクタはありません。

### `Index(string listType = "confirmed")`

- HTTP：GET
- 引数：`confirmed`、`temporary`、`completed`のいずれか
- 戻り値：`Index.cshtml`と`List<ReservationDetailViewModel>`
- 呼び出し元：既定ルート、各処理後のリダイレクト、一覧タブ
- 次に呼ぶもの：`GetReservationsForList`、`ReservationListService.GetFacilities`
- 処理順：不明な一覧種別を`confirmed`へ直し、一覧を取得します。拠点一覧を`ViewData["Facilities"]`、選択中タブを`ViewData["SelectedListType"]`へ設定します。

### `Cancel(int reservationId, string listType = "confirmed")`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：成功時は同じ一覧種別へリダイレクト、失敗時は`Index.cshtml`
- 呼び出し元：本予約・仮予約一覧のキャンセルフォーム
- 次に呼ぶもの：`ReservationService.CancelReservation`
- 処理：一覧種別を確認してServiceへキャンセルを依頼します。失敗時はIndexに必要な予約一覧、拠点一覧、選択中タブを設定し直します。

### `GetReservationsForList(string listType)`

- 種類：private補助メソッド
- 戻り値：`List<ReservationDetailViewModel>`
- 呼び出し元：`Index`、`Cancel`のエラー処理
- 次に呼ぶもの：一覧種別に応じて次のいずれか

| listType | 呼ぶServiceメソッド |
|---|---|
| `temporary` | `GetReservationList(U001, Tentative)` |
| `completed` | `GetCompletedReservations(U001)` |
| その他 | `GetCurrentConfirmedReservations(U001)` |

### `TemporaryInput(int reservationId)`

- HTTP：GET
- 戻り値：`TemporaryInput.cshtml`、見つからなければ404
- 呼び出し元：仮予約一覧の「予約確定」
- 次に呼ぶもの：`FindReservationDetail`、`ToInputViewModel`
- 処理：U001のTentative予約を探し、入力用ViewModelへ変換して実際の入力Viewを直接表示します。

### `TemporaryConfirm(int reservationId, ReservationInputViewModel model)`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：正常時は`TemporaryConfirm.cshtml`、入力エラー時は`TemporaryInput.cshtml`
- 呼び出し元：仮予約確定入力画面
- 次に呼ぶもの：Serviceは呼ばない
- 処理：入力検証後、予約IDをViewModelへ設定して確認画面へ渡します。

### `ConfirmTemporary(int reservationId, ReservationInputViewModel model)`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：成功時はIndexへリダイレクト、失敗時は`TemporaryConfirm.cshtml`
- 呼び出し元：仮予約確定確認画面
- 次に呼ぶもの：`ReservationService.ConfirmTemporaryReservation`
- 処理：既存のTentative予約をConfirmedへ更新するようServiceへ依頼します。新しい予約は作りません。

### `Edit(int reservationId)`

- HTTP：GET
- 戻り値：`Edit.cshtml`、見つからなければ404
- 呼び出し元：本予約一覧の「変更」
- 次に呼ぶもの：`FindReservationDetail`、`ToInputViewModel`
- 処理：U001のConfirmed予約を探し、現在値を入力用ViewModelへ移して変更画面を直接表示します。

### `EditConfirm(ReservationInputViewModel model)`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：正常時は`EditConfirm.cshtml`、入力エラー時は`Edit.cshtml`
- 呼び出し元：予約変更入力画面
- 次に呼ぶもの：Serviceは呼ばない
- 処理：入力内容を検証し、変更確認画面へ渡します。

### `Update(ReservationInputViewModel model)`

- HTTP：POST
- セキュリティ：`ValidateAntiForgeryToken`
- 戻り値：成功時はIndexへリダイレクト、失敗時は`EditConfirm.cshtml`
- 呼び出し元：予約変更確認画面
- 次に呼ぶもの：`ReservationService.UpdateReservation`
- 処理：Serviceへ既存Confirmed予約の更新を依頼します。

### `FindReservationDetail(int reservationId, ReservationStatus status)`

- 種類：private補助メソッド
- 戻り値：一致した`ReservationDetailViewModel`。なければnull
- 呼び出し元：`TemporaryInput`、`Edit`
- 次に呼ぶもの：`ReservationListService.GetReservationList`
- 処理：U001と状態で一覧を取得し、その中から予約IDが一致する1件を探します。

### `ToInputViewModel(ReservationDetailViewModel detail)`

- 種類：private static補助メソッド
- 戻り値：`ReservationInputViewModel`
- 呼び出し元：`TemporaryInput`、`Edit`
- 処理：一覧・詳細用の値を入力画面用の形へ詰め替えます。`RoomId`は変更処理に必要な内部値です。

## 中継Viewについて

Controllerは現在、`TemporaryInput.cshtml`、`TemporaryConfirm.cshtml`、`Edit.cshtml`、`EditConfirm.cshtml`を直接指定します。以前の中継Viewである`Input.cshtml`、`ConfirmTemporary.cshtml`、`ConfirmUpdate.cshtml`は`Views/ReservationList`には存在しません。
