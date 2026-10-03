# 38 - Check-up reminder (branch `feat/checkup-reminder`)

The check-up report (spec 16) only helps if it is actually sent. This milestone adds an optional
reminder, shown as a tray balloon, that nudges the person to make a check-up and send it to their
family helper. Porchlight still never sends anything itself: clicking the reminder only opens the
Get help page, where the report is made.

## Goals

- Settings > Notifications gets a "Check-up reminder" card:
  - checkbox "Remind me to send a check-up report" (default **off**),
  - frequency: Every week / Every 2 weeks / Every month (default every week),
  - day of the week (default Sunday).
- While Porchlight runs (window open or in the tray) a background timer checks every 15 minutes
  (first check 2 minutes after start). When a reminder is due it shows one balloon ("Time for a
  check-up"); clicking it opens the Get help page.
- The reminder waits a full period after a report or a reminder. If a report was made within the
  period, no reminder is shown.
- Missed reminders (PC off, app closed) show **once** when Porchlight next runs, never once per
  missed period.
- The check-up card on the Get help page shows "Last check-up: <date>" (after the first report)
  and "Next reminder: <date>" (while reminders are on).

## Non-goals

- Sending, emailing or uploading anything automatically.
- A time of day (the reminder fires on the first check on the due day), notification actions such as
  "Snooze till tomorrow": the existing tray balloon (`ITrayIcon.ShowBalloon`) supports only a click
  action, so a click just opens the page and an ignored reminder simply waits for the next period.
- Reminding while Porchlight is not running.

## Design

### Settings (`Porchlight.Core.Settings.CheckupReminderSettings`, `AppSettings.CheckupReminder`)

`Enabled`, `Frequency` (`CheckupReminderFrequency`: Weekly, EveryTwoWeeks, Monthly), `Day`
(`DayOfWeek`), `EnabledSinceUtc` (stamped when turned on, cleared when turned off),
`LastReminderShownUtc`, `LastReportCreatedUtc`. Written only through `ISettingsStore.Update`.

### Scheduler (`Porchlight.Core.Checkup.CheckupReminderScheduler`, pure, takes `TimeProvider`)

All maths is on local calendar dates (`DateOnly`, via `TimeProvider.GetLocalNow()` /
`LocalTimeZone`), never on added hours, so daylight-saving changes cannot move a reminder.

- `NextReminderDate(settings)`: null when off. The period start is the later of the last report and
  the last reminder; the next date is the first chosen weekday on or after period start + one period
  (7 days, 14 days, or one calendar month with end-of-month clamping), and never before the day the
  reminder was turned on. With no report or reminder yet it is the first chosen weekday on or after
  the day it was turned on.
- `IsDue(settings)`: `today >= NextReminderDate`. An overdue reminder is due once; showing it sets
  `LastReminderShownUtc`, which moves the period start to today.
- `LastReportDate(settings)`: local day of the last report, for display.

### Runtime (`Porchlight.App.Features.Notifications.CheckupReminderHostedService`)

Hosted service modelled on `AlertHostedService`: initial delay, then a `PeriodicTimer`. `CheckOnce`
asks the scheduler, shows the balloon (click navigates to `RemoteSupportViewModel`) and records
`LastReminderShownUtc`. Errors are logged and the loop continues. In DEBUG demo mode the loop does
not run, so demo runs never raise real toasts (same as alerts). Registered in
`AddNotificationsFeature`; the scheduler is registered in `AddCheckup`, both outside any
`#if DEBUG` branch.

### Recording a report (`CheckupCardViewModel`)

`LastReportCreatedUtc` is set when the person creates the report and again when they copy, save or
email it (successful actions only). The card's `LastCheckupText` / `NextReminderText` refresh on
those actions and when the page is shown. An overdue "next reminder" is displayed as today.

### Text

- Checkbox: "Remind me to send a check-up report".
- Balloon: "Time for a check-up" / "Send a quick check-up of this PC to the person who helps you. Click
  here to make one."
- Card lines: "Last check-up: Fri 2 Oct 2026", "Next reminder: Sun 4 Oct 2026". (Porchlight cannot know a
  report was actually sent, so the line says "check-up", not "sent".)

## Tests

- `CheckupReminderSchedulerTests` (Core): off, first reminder, same-day enable, one period after a
  reminder, skipping after a recent report, old report ignored, the three frequencies, month-end
  clamping, missed periods fire once, re-enable after a long gap, local-date vs UTC, DST fall-back.
- `CheckupReminderHostedServiceTests`: off shows nothing, due shows one balloon and records it, no
  repeat within the period, recent report suppresses it, the loop honours the initial delay.
- `CheckupCardViewModelTests`: report actions record the time, lines show/hide.
- `NotificationSettingsViewModelTests`: defaults, persistence, no write on load.
- `AppCompositionTests` (real container, `ValidateOnBuild`) covers the new registrations.

## Acceptance criteria

- [ ] Reminder is off by default and configurable (on/off, frequency, weekday) on Settings > Notifications.
- [ ] When due and the app is running, one balloon appears; clicking opens Get help.
- [ ] No reminder within one period of a created/copied/saved/emailed report.
- [ ] After the PC was off for several periods, exactly one reminder is shown.
- [ ] Get help shows "Last check-up" and "Next reminder" as described.
- [ ] Nothing is ever sent by Porchlight.
- [ ] `dotnet build -c Release` has no warnings; `dotnet test -c Release` passes.
