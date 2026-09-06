using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ReservationManagementApp.DataAccess;
using ReservationManagementApp.Models.Entities;
using ReservationManagementApp.Models.ViewModels;
using ReservationManagementApp.Services;

namespace ReservationManagementApp.Controllers
{
    public class ReservationListController : Controller
    {
        private const string CurrentUserId = "U001";
        private const string ConfirmedListType = "confirmed";
        private const string TemporaryListType = "temporary";
        private const string CompletedListType = "completed";

        private readonly ReservationListService _reservationListService;
        private readonly ReservationService _reservationService;

        public ReservationListController()
        {
            _reservationListService = new ReservationListService(
                new SQLiteConnectionFactory());
            _reservationService = new ReservationService(
                new SQLiteConnectionFactory());
        }

        [HttpGet]
        public ActionResult Index(string listType = ConfirmedListType)
        {
            if (listType != TemporaryListType && listType != CompletedListType)
            {
                listType = ConfirmedListType;
            }

            var reservations = GetReservationsForList(listType);

            ViewData["Facilities"] = _reservationListService.GetFacilities();
            ViewData["SelectedListType"] = listType;
            return View(reservations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(
            int reservationId,
            string listType = ConfirmedListType)
        {
            if (listType != TemporaryListType && listType != CompletedListType)
            {
                listType = ConfirmedListType;
            }

            if (!_reservationService.CancelReservation(reservationId, CurrentUserId))
            {
                ModelState.AddModelError(string.Empty, "予約をキャンセルできませんでした。");
                ViewData["Facilities"] = _reservationListService.GetFacilities();
                ViewData["SelectedListType"] = listType;
                return View(
                    "Index",
                    GetReservationsForList(listType));
            }

            return RedirectToAction("Index", new { listType = listType });
        }

        private List<ReservationDetailViewModel> GetReservationsForList(
            string listType)
        {
            if (listType == TemporaryListType)
            {
                return _reservationListService.GetReservationList(
                    CurrentUserId,
                    ReservationStatus.Tentative);
            }

            if (listType == CompletedListType)
            {
                return _reservationListService.GetCompletedReservations(CurrentUserId);
            }

            return _reservationListService.GetCurrentConfirmedReservations(CurrentUserId);
        }

        [HttpGet]
        public ActionResult TemporaryInput(int reservationId)
        {
            var detail = FindReservationDetail(
                reservationId,
                ReservationStatus.Tentative);

            if (detail == null)
            {
                return HttpNotFound();
            }

            return View("TemporaryInput", ToInputViewModel(detail));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TemporaryConfirm(
            int reservationId,
            ReservationInputViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("TemporaryInput", model);
            }

            model.ReservationId = reservationId;
            return View("TemporaryConfirm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmTemporary(
            int reservationId,
            ReservationInputViewModel model)
        {
            if (!ModelState.IsValid
                || !_reservationService.ConfirmTemporaryReservation(
                    reservationId,
                    model,
                    CurrentUserId))
            {
                ModelState.AddModelError(string.Empty, "仮予約を本予約へ確定できませんでした。");
                return View("TemporaryConfirm", model);
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int reservationId)
        {
            var detail = FindReservationDetail(
                reservationId,
                ReservationStatus.Confirmed);

            if (detail == null)
            {
                return HttpNotFound();
            }

            return View(ToInputViewModel(detail));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditConfirm(ReservationInputViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", model);
            }

            return View("EditConfirm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(ReservationInputViewModel model)
        {
            if (!ModelState.IsValid
                || !_reservationService.UpdateReservation(model, CurrentUserId))
            {
                ModelState.AddModelError(string.Empty, "予約を変更できませんでした。");
                return View("EditConfirm", model);
            }

            return RedirectToAction("Index");
        }

        private ReservationDetailViewModel FindReservationDetail(
            int reservationId,
            ReservationStatus status)
        {
            return _reservationListService
                .GetReservationList(CurrentUserId, status)
                .FirstOrDefault(reservation => reservation.ReservationId == reservationId);
        }

        private static ReservationInputViewModel ToInputViewModel(
            ReservationDetailViewModel detail)
        {
            return new ReservationInputViewModel
            {
                ReservationId = detail.ReservationId,
                FacilityName = detail.FacilityName,
                RoomId = detail.RoomId,
                RoomName = detail.RoomName,
                Date = detail.StartDateTime.Date,
                StartTime = detail.StartDateTime.TimeOfDay,
                EndTime = detail.EndDateTime.TimeOfDay,
                Title = detail.Title,
                Participants = detail.Participants,
                Remarks = detail.Remarks,
                TotalPrice = detail.TotalPrice
            };
        }

    }
}
