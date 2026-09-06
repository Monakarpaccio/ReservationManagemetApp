using System;
using System.Collections.Generic;
using System.Web.Mvc;
using ReservationManagementApp.DataAccess;
using ReservationManagementApp.Models.Entities;
using ReservationManagementApp.Models.ViewModels;
using ReservationManagementApp.Services;

namespace ReservationManagementApp.Controllers
{
    public class ReservationController : Controller
    {
        private const string CurrentUserId = "U001";

        private readonly ReservationSearchService _reservationSearchService;
        private readonly ReservationService _reservationService;

        public ReservationController()
        {
            _reservationSearchService = new ReservationSearchService(
                new SQLiteConnectionFactory());
            _reservationService = new ReservationService(
                new SQLiteConnectionFactory());
        }

        [HttpGet]
        public ActionResult Search(string facilityId, DateTime? date, int? capacity)
        {
            if (!date.HasValue)
            {
                ModelState.AddModelError("date", "日付を指定してください。");
                return View((ReservationScheduleViewModel)null);
            }

            var schedule = _reservationSearchService.GetReservationSchedule(
                facilityId,
                date.Value,
                capacity);

            if (schedule == null)
            {
                ModelState.AddModelError(string.Empty, "会議室の予約状況を取得できませんでした。");
            }

            return View(schedule);
        }

        [HttpGet]
        public ActionResult TemporarySearch(
            string facilityId,
            DateTime? date,
            TimeSpan? startTime,
            TimeSpan? endTime,
            int? capacity)
        {
            var rooms = new List<Room>();
            ViewData["FacilityId"] = facilityId;
            ViewData["Date"] = date;
            ViewData["StartTime"] = startTime;
            ViewData["EndTime"] = endTime;
            ViewData["Capacity"] = capacity;

            if (!date.HasValue || !startTime.HasValue || !endTime.HasValue)
            {
                ModelState.AddModelError(string.Empty, "日付、開始時刻、終了時刻を指定してください。");
                return View(rooms);
            }

            if (!_reservationSearchService.CanCreateTemporaryReservation(CurrentUserId))
            {
                ModelState.AddModelError(string.Empty, "仮予約の検索を実行できませんでした。");
                return View(rooms);
            }

            rooms = _reservationSearchService.SearchAvailableRoomsForTemporary(
                facilityId,
                date.Value,
                startTime.Value,
                endTime.Value,
                capacity,
                CurrentUserId);

            return View(rooms);
        }

        [HttpGet]
        public ActionResult Input(
            string roomId,
            string roomName,
            string facilityName,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            var model = new ReservationInputViewModel
            {
                RoomId = roomId,
                RoomName = roomName,
                FacilityName = facilityName,
                Date = date,
                StartTime = startTime,
                EndTime = endTime
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(ReservationInputViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Input", model);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ReservationInputViewModel model)
        {
            if (!ModelState.IsValid
                || !_reservationService.CreateReservation(model, CurrentUserId))
            {
                ModelState.AddModelError(string.Empty, "予約を登録できませんでした。");
                return View("Confirm", model);
            }

            return RedirectToAction("Index", "ReservationList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTemporary(
            string roomId,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            if (!_reservationService.CreateTemporaryReservation(
                roomId,
                date,
                startTime,
                endTime,
                CurrentUserId))
            {
                ModelState.AddModelError(string.Empty, "仮予約を登録できませんでした。");
                ViewData["Date"] = date;
                ViewData["StartTime"] = startTime;
                ViewData["EndTime"] = endTime;
                return View("TemporarySearch", new List<Room>());
            }

            return RedirectToAction("Index", "ReservationList");
        }

    }
}
