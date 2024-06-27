using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class MyDbInitializer
{
    public static async Task InitializeAsync(MyDbContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        await context.Database.OpenConnectionAsync();

        try
        {
            var sqlQuery = @"
                SELECT
                    p.ID AS OrderId,
                    p.post_date AS OrderDate,
                    pm1.meta_value AS TrackingCode,
                    oim.order_item_name AS ItemName,
                    u.user_email AS UserEmail,
                    u.display_name AS UserName
                FROM
                    wp_posts AS p
                JOIN
                    wp_postmeta AS pm1 ON p.ID = pm1.post_id
                JOIN
                    wp_woocommerce_order_items AS oim ON p.ID = oim.order_id
                JOIN
                    wp_woocommerce_order_itemmeta AS oim_meta ON oim.order_item_id = oim_meta.order_item_id
                JOIN
                    wp_postmeta AS pm2 ON p.ID = pm2.post_id AND pm2.meta_key = '_customer_user'
                JOIN
                    wp_users AS u ON pm2.meta_value = u.ID
                WHERE
                    p.post_type = 'shop_order'
                    AND p.post_status IN ('wc-processing', 'wc-completed')
                    AND pm1.meta_key = '_correios_tracking_code'
                    AND oim_meta.meta_key = '_product_id'
                ORDER BY
                    p.post_date DESC
                LIMIT 10;";

            var orderDetails = await context.Order
                .FromSqlRaw(sqlQuery)
                .ToListAsync();

            var teste = 0;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
