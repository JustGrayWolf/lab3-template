﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Gateway.Services;
using Gateway.ServiceInterfaces;
using Gateway.DTO;
using Gateway.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;

namespace Gateway.Controllers
{
    [ApiController]
    [Route("/")]
    public class GatewayController : ControllerBase
    {
        private readonly IPaymentService _paymentsService;
        private readonly ILoyaltyService _loyaltyService;
        private readonly IReservationService _reservationsService;
        

        public GatewayController(IReservationService reservationsService,
            IPaymentService paymentsService, ILoyaltyService loyaltyService)
        {
            _loyaltyService = loyaltyService;
            _reservationsService = reservationsService;
            _paymentsService = paymentsService;
           
        }

        [IgnoreAntiforgeryToken]
        [HttpGet("manage/health")]
        public async Task<ActionResult> HealthCheck()
        {
            return Ok();
        }

 

        [HttpGet("api/v1/me")]
        public async Task<ActionResult<UserInfoResponse?>> GetUserInfoByUsername(
        [FromHeader(Name = "X-User-Name")] string xUserName)
        {
            var checkReservationService = _reservationsService.HealthCheckAsync();

            if (!(await checkReservationService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var reservations = await _reservationsService.GetReservationsByUsernameAsync(xUserName);
            if (reservations == null || !reservations.Any())
            {
                return null;
            }

            var response = new UserInfoResponse();
            response.Reservations = new List<UserReservationInfo>();
            var tasks = reservations.Select(reservation => Task.Run(async () =>
            {
                var hotel = await _reservationsService.GetHotelsByIdAsync(reservation.HotelId);
                Payment payment = null;
                var checkPaymentService = _paymentsService.HealthCheckAsync();
                if (await checkPaymentService)
                {
                    payment = await _paymentsService.GetPaymentByUidAsync(reservation.PaymentUid);
                }
                //var payment = await _paymentsService.GetPaymentByUidAsync(reservation.PaymentUid);

                response.Reservations.Add(new UserReservationInfo()
                {
                    ReservationUid = reservation.ReservationUid,
                    Status = reservation.Status,
                    StartDate = DateOnly.FromDateTime(reservation.StartDate),
                    EndDate = DateOnly.FromDateTime(reservation.EndDate),
                    Hotel = new HotelInfo()
                    {
                        HotelUid = hotel.HotelUid,
                        Name = hotel.Name,
                        FullAddress = hotel.Country + ", " + hotel.City + ", " + hotel.Address,
                        Stars = hotel.Stars,
                    },
                    Payment = payment == null ? new object[] { } : new PaymentInfo()
                    {
                        Status = payment?.Status,
                        Price = payment?.Price,
                    },
                });
            }));

            await Task.WhenAll(tasks);


            Loyalty loyalty = null;
            var checkLoyaltyService = _loyaltyService.HealthCheckAsync();
            if (await checkLoyaltyService)
            {
                loyalty = await _loyaltyService.GetLoyaltyByUsernameAsync(xUserName);

            }

            response.Loyalty = loyalty == null ? new object[]{} : new LoyaltyInfo()
            {
                Status = loyalty?.Status,
                Discount = loyalty?.Discount,
            };

            return response;
        }

        [HttpGet("api/v1/reservations")]
        public async Task<ActionResult<List<UserReservationInfo>?>> GetReservationsInfoByUsername(
        [FromHeader(Name = "X-User-Name")] string xUserName)
        {
            var checkReservationService = _reservationsService.HealthCheckAsync();

            if (!(await checkReservationService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var reservations = await _reservationsService.GetReservationsByUsernameAsync(xUserName);
            if (reservations == null || !reservations.Any())
            {
                return null;
            }

            var response = new List<UserReservationInfo>();
            var tasks = reservations.Select(reservation => Task.Run(async () =>
            {
                var hotel = await _reservationsService.GetHotelsByIdAsync(reservation.HotelId);
                Payment payment = null;
                var checkPaymentService = _paymentsService.HealthCheckAsync();
                if (await checkPaymentService)
                {
                    payment = await _paymentsService.GetPaymentByUidAsync(reservation.PaymentUid);
                }

                response.Add(new UserReservationInfo()
                {
                    ReservationUid = reservation.ReservationUid,
                    Status = reservation.Status,
                    StartDate = DateOnly.FromDateTime(reservation.StartDate),
                    EndDate = DateOnly.FromDateTime(reservation.EndDate),
                    Hotel = new HotelInfo()
                    {
                        HotelUid = hotel.HotelUid,
                        Name = hotel.Name,
                        FullAddress = hotel.Country + ", " + hotel.City + ", " + hotel.Address,
                        Stars = hotel.Stars,
                    },
                    Payment = new PaymentInfo()
                    {
                        Status = payment?.Status,
                        Price = payment?.Price,
                    },
                });
            }));

            
            await Task.WhenAll(tasks);

            return response;
        }

        [HttpGet("api/v1/loyalty")]
        public async Task<ActionResult<LoyaltyInfoResponse?>> GetLoyaltyInfoByUsername(
[FromHeader(Name = "X-User-Name")] string xUserName)
        {
            var checkLoyaltyService = _loyaltyService.HealthCheckAsync();

            if (!(await checkLoyaltyService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Loyalty Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }
            var loyalty = await _loyaltyService.GetLoyaltyByUsernameAsync(xUserName);

            var response = new LoyaltyInfoResponse()
            {
                Status = loyalty.Status,
                Discount = loyalty.Discount,
                ReservationCount = loyalty.ReservationCount,
            };

            return response;
        }

        [HttpGet("api/v1/reservations/{reservationsUid}")]
        public async Task<ActionResult<UserReservationInfo>?> GetReservationsInfoByUsername(
        [FromRoute] Guid reservationsUid,
        [FromHeader(Name = "X-User-Name")] string xUserName)
        {
            var checkReservationService = _reservationsService.HealthCheckAsync();

            if (!(await checkReservationService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var reservation = await _reservationsService.GetReservationsByUidAsync(reservationsUid);
            if (reservation == null)
            {
                return BadRequest();
            }

            if (!reservation.Username.Equals(xUserName))
            {
                return BadRequest();
            }

            var hotel = await _reservationsService.GetHotelsByIdAsync(reservation.HotelId);

            var checkPaymentService = _paymentsService.HealthCheckAsync();

            Payment payment = null;

            if (await checkPaymentService)
            {
                payment = await _paymentsService.GetPaymentByUidAsync(reservation.PaymentUid);
            }

            var response = new UserReservationInfo()
            {
                ReservationUid = reservation.ReservationUid,
                Status = reservation.Status,
                StartDate = DateOnly.FromDateTime(reservation.StartDate),
                EndDate = DateOnly.FromDateTime(reservation.EndDate),
                Hotel = new HotelInfo()
                {
                    HotelUid = hotel.HotelUid,
                    Name = hotel.Name,
                    FullAddress = hotel.Country + ", " + hotel.City + ", " + hotel.Address,
                    Stars = hotel.Stars,
                },
                Payment = new PaymentInfo()
                {
                    Status = payment?.Status,
                    Price = payment?.Price,
                },
            };


            return response;
        }

        [HttpPost("api/v1/reservations")]
        public async Task<ActionResult<CreateReservationResponse?>> CreateReservation(
        [FromHeader(Name = "X-User-Name")] string xUserName,
        [FromBody] CreateReservationRequest request)
        {
            var checkReservationService = _reservationsService.HealthCheckAsync();

            if (!(await checkReservationService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var hotel = await _reservationsService.GetHotelsByUidAsync(request.HotelUid);

            if (hotel == null)
            {
                return BadRequest(null);
            }

            int sum = ((request.EndDate - request.StartDate).Days) * hotel.Price;

            var checkLoyaltyService = _loyaltyService.HealthCheckAsync();

            if (!(await checkLoyaltyService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Loyalty Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var loyalty = await _loyaltyService.GetLoyaltyByUsernameAsync(xUserName);


            if (loyalty == null)
            {
                sum -= sum * 5 / 100;
            }
            else
            {
                sum -= sum * loyalty.Discount / 100;
            }

            Payment paymentRequest = new Payment()
            {
                Price = sum,
            };

            var checkPaymentService = _paymentsService.HealthCheckAsync();

            if (!(await checkPaymentService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Payment Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var payment = await _paymentsService.CreatePaymentAsync(paymentRequest);

            if (payment == null)
            {
                return BadRequest(null);
            }

            checkLoyaltyService = _loyaltyService.HealthCheckAsync();

            if (!(await checkLoyaltyService))
            {
                await _paymentsService.RollBackPayment(payment.PaymentUid);
                var resp = new ErrorResponse()
                {
                    Message = "Loyalty Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            loyalty = await _loyaltyService.PutLoyaltyByUsernameAsync(xUserName);

            Reservation reservationRequest = new Reservation()
            {
                PaymentUid = payment.PaymentUid,
                HotelId = hotel.Id,
                EndDate = request.EndDate,
                StartDate = request.StartDate,
            };

            var reservation = await _reservationsService.CreateReservationAsync(xUserName, reservationRequest);

            var reservationResponse = new CreateReservationResponse()
            {
                ReservationUid = reservation.ReservationUid,
                HotelUid = hotel.HotelUid,
                //StartDate = DateOnly.FromDateTime(reservation.StartDate),
                //EndDate = DateOnly.FromDateTime(reservation.EndDate),
                StartDate = DateOnly.FromDateTime(reservation.StartDate),
                EndDate = DateOnly.FromDateTime(reservation.EndDate),
                Status = reservation.Status,
                Discount = loyalty.Discount,
                Payment = new PaymentInfo()
                {
                    Status = payment.Status,
                    Price = payment.Price,
                }
            };

            return Ok(reservationResponse);
        }

        [HttpGet("api/v1/hotels")]
        public async Task<ActionResult<PaginationResponse<IEnumerable<Hotels>>?>> GetAllHotels(
 [FromQuery] int? page,
 [FromQuery] int? size)
        {
            var checkReservationService = _reservationsService.HealthCheckAsync();

            if (!(await checkReservationService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }
            var response = await _reservationsService.GetHotelsAsync(page, size);
            return response;
        }

        [HttpDelete("api/v1/reservations/{reservationsUid}")]
        public async Task<ActionResult> DeleteReservationsByUid(
        [FromRoute] Guid reservationsUid,
        [FromHeader(Name = "X-User-Name")] string xUserName)
        {
            var checkReservationService = _reservationsService.HealthCheckAsync();

            if (!(await checkReservationService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var reservation = await _reservationsService.GetReservationsByUidAsync(reservationsUid);
            if (reservation == null)
            {
                return BadRequest();
            }

            var updateReservationTask = _reservationsService.DeleteReservationAsync(reservation.ReservationUid);

            var checkPaymentService = _paymentsService.HealthCheckAsync();

            if (!(await checkPaymentService))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Payment Service unavailable",
                };
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var updatePaymentTask = _paymentsService.CancelPaymentByUidAsync(reservation.PaymentUid);

            var updateLoyaltyTask = _loyaltyService.DeleteLoyaltyByUsernameAsync(xUserName);

            await updateReservationTask;
            await updatePaymentTask;
            await updateLoyaltyTask;

            return Ok(null);
        }




    }
}
