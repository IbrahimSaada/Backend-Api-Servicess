## 🚀 **Backend-API-Services: A Scalable Social Media Backend Solution**

Welcome to **Backend-API-Services**, a robust backend solution designed specifically for **social media platforms**. This backend provides reliable APIs, scalable infrastructure, and integrated third-party services to power a seamless experience for mobile and web applications.

---

## 📚 **Overview**

This backend serves as the core API layer for a social media platform where users can:
- **Share Posts** with photos, videos, and text.  
- **Engage in Conversations** via chats and comments.  
- **Bookmark Favorite Content** for later access.  
- **Receive Notifications** for updates and interactions.  
- **Manage User Profiles** with secure authentication.  
- **Real-Time Chat** between users.  
- **Like, Share, and Comment** on posts.  
- **Stories and Story Viewing** features.

The backend is hosted and managed using **AWS Elastic Beanstalk** and integrates **AWS services (EC2, RDS, S3, Lambda)** along with **Google Firebase** for **push notifications**.

### 📢 **Public and Private Features**
- **Public Features:** Available for all users, including sharing posts, real-time chats, commenting, liking, and story interactions.
- **Private Features:** Additional functionalities will be available when the app is officially published on the **Google Play Store** and **Apple App Store**.

---

## 🛠️ **Tech Stack**

- **Backend Framework:** ASP.NET Core  
- **Database:** Amazon RDS (PostgreSQL)  
- **File Storage:** Amazon S3  
- **Hosting Platform:** AWS Elastic Beanstalk  
- **Authentication:** JWT (JSON Web Token) Authentication  
- **Push Notifications:** Google Firebase  
- **Real-time Updates:** AWS Lambda (for serverless event handling)  
- **Deployment Pipeline:** AWS CodePipeline  
- **Monitoring & Logging:** AWS CloudWatch  

---

## 📦 **Architecture Overview**

```
+-------------------+        +-----------------+       +-------------+  
|  Mobile/Web App   | -----> | Backend APIs    | ----> | AWS RDS      |  
+-------------------+        +-----------------+       +-------------+  
                                   |                       ^  
                                   v                       |  
                            +-----------------+       +-------------+  
                            | AWS Lambda      | ----> | AWS S3       |  
                            +-----------------+       +-------------+  
                                   |  
                                   v  
                            +-----------------+  
                            | Google Firebase |  
                            +-----------------+  
```

---

## 📑 **Prerequisites**

### 🔑 **AWS Services Setup**
- **Elastic Beanstalk:** For hosting the ASP.NET Core backend.
- **EC2 Instance:** Managed under Elastic Beanstalk for server infrastructure.
- **RDS (PostgreSQL):** Database management.
- **S3 Bucket:** For storing uploaded images, videos, and other media files.
- **AWS Lambda:** For asynchronous tasks, notifications, and serverless operations.
- **CloudWatch:** For monitoring and logs.

### 🔑 **Firebase Setup**
- **Firebase Cloud Messaging (FCM):** For push notifications.
- **Firebase Admin SDK Key:** Needed for backend integration.

### 📥 **Tools & Dependencies**
- .NET SDK (latest version)  
- AWS CLI  
- Git  
- Visual Studio or VS Code  

---

## 🚀 **Setup and Deployment**

### 1️⃣ **Clone the Repository**
```bash
git clone https://github.com/IbrahimSaada/Backend-Api-Servicess.git
cd Backend-Api-services
```

### 2️⃣ **Setup Environment Variables**
Create an `appsettings.Development.json` file with your AWS and Firebase credentials:

```json
{
  "Jwt": {
    "Key": "<YourJWTKey>",
    "Issuer": "<YourIssuer>",
    "Audience": "<YourAudience>",
    "AccessTokenLifetime": "120",
    "RefreshTokenLifetime": "10080"
  },
  "AppSecretKey": "<YourAppSecretKey>",
  "ConnectionStrings": {
    "DefaultConnection": "<YourRDSConnectionString>"
  },
  "AWS": {
    "Region": "<YourAWSRegion>",
    "AccessKey": "<YourAWSAccessKey>",
    "SecretKey": "<YourAWSSecretKey>",
    "BucketName": "<YourS3BucketName>"
  },
  "Firebase": {
    "AdminSDKKey": "<YourFirebaseAdminKey>"
  }
}
```

### 3️⃣ **Restore Dependencies**
```bash
dotnet restore
```

### 4️⃣ **Run Locally**
```bash
dotnet run
```

Access the local API at `https://localhost:5001`.

### 5️⃣ **Deploy to AWS Elastic Beanstalk**
1. Configure the AWS CLI:
```bash
aws configure
```
2. Initialize Elastic Beanstalk application:
```bash
eb init
```
3. Deploy:
```bash
eb create Backend-Api-Env
```

---

## 🔑 **API Endpoints Overview**

| Endpoint                 | Method | Description           |
|---------------------------|--------|-----------------------|
| `/api/auth/login`        | POST   | User login            |
| `/api/auth/register`     | POST   | User registration     |
| `/api/posts`             | GET    | Get all posts         |
| `/api/posts/{id}`        | GET    | Get a specific post   |
| `/api/posts`             | POST   | Create a new post     |
| `/api/comments/{postId}` | GET    | Get comments on a post|
| `/api/notifications`     | GET    | Get notifications     |
etc..........................................................

---

## 📄 **License**

This project is licensed under the **MIT License**.

---

## 📬 **Contact & Support**

- **Email:** ibrahimsaada99@gmail.com ahmadghosen20@gmail.com

