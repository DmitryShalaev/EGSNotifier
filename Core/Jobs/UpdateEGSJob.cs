using Core.DB;
using Core.Parser;

using Quartz;
using Quartz.Impl;

namespace Core.Jobs {
    public class UpdateEGSJob : IJob {

        public static async Task StartAsync() {
            int hour = -1;
            using(ScheduleDbContext dbContext = new()) {
                await EGSParser.UpdatingEGS(dbContext);
                hour = dbContext.EGS.OrderBy(i => i.ID).Last().EndDate.ToLocalTime().Hour + 1;
            }

            // Создание фабрики планировщиков
            var schedulerFactory = new StdSchedulerFactory();
            // Получение планировщика
            IScheduler scheduler = await schedulerFactory.GetScheduler();

            // Определение задания UpdateEGSJob
            IJobDetail job = JobBuilder.Create<UpdateEGSJob>()
                .WithIdentity("UpdateEGSJob", "group1") // Уникальный идентификатор задания
                .Build();

            // Определение триггера для запуска задания каждый час
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("UpdateEGSJobTrigger", "group1") // Уникальный идентификатор триггера
                .StartNow() // Запуск задания сразу после старта планировщика
                .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(hour == -1 ? 20 : hour, 30))
                .Build();

            // Запуск планировщика
            await scheduler.Start();
            // Назначение задания и триггера в планировщике
            await scheduler.ScheduleJob(job, trigger);
        }

        async Task IJob.Execute(IJobExecutionContext context) {
            using(ScheduleDbContext dbContext = new())
                await EGSParser.UpdatingEGS(dbContext);
        }
    }
}
