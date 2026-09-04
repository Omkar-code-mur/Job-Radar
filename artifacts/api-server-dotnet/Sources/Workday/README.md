# Workday source

`WorkdayJobSource` consumes a public Workday CXS jobs endpoint using POST requests.

Configure `JobSource.Url` as the complete CXS endpoint, for example:

`https://synechron.wd1.myworkdayjobs.com/wday/cxs/synechron/SynechronCareers/jobs`

The adapter uses paginated requests with `limit`, `offset`, `searchText`, and `appliedFacets`, and does not require credentials for public career sites.
