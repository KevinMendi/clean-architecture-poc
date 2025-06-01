using Bookify.Domain.Users;

namespace Bookify.Infrastructure.Repositories
{
    internal sealed class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext dbContext)
            : base(dbContext)
        {
        }

        public override void Add(User user)
        {
            foreach (var role in user.Roles)
            {
                // This will tell efcore that any roles present on User object are already inside of the database
                // and no need to insert them again
                DbContext.Attach(role);
            }

            DbContext.Add(user);
        }
    }
}
