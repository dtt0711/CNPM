import { useTranslation } from "react-i18next";

export interface GoogleStyleRatingProps {
  rating: number;
  reviewCount: number;
  className?: string;
  /** Show “reviews” label after the count (Maps place panel style). */
  showReviewsLabel?: boolean;
  size?: "sm" | "md";
}

/** Google Maps–like rating: 4.4 ★★★★☆ (29.775) */
export function GoogleStyleRating({
  rating,
  reviewCount,
  className = "",
  showReviewsLabel = false,
  size = "sm",
}: GoogleStyleRatingProps) {
  const { t, i18n } = useTranslation();
  const locale = i18n.language || "vi";
  const safeRating = Number.isFinite(rating) ? Math.min(5, Math.max(0, rating)) : 0;
  const score = safeRating.toFixed(1);
  const countLabel = reviewCount.toLocaleString(locale);

  const label = showReviewsLabel
    ? t("places.ratingWithLabel", { rating: score, count: countLabel })
    : t("places.rating", { rating: score, count: countLabel });

  return (
    <div
      className={`gmaps-rating gmaps-rating--${size} ${className}`.trim()}
      role="img"
      aria-label={label}
    >
      <span className="gmaps-rating__score">{score}</span>
      <span className="gmaps-rating__stars" aria-hidden="true">
        {Array.from({ length: 5 }, (_, index) => {
          const fill = Math.min(1, Math.max(0, safeRating - index));
          return (
            <span key={index} className="gmaps-rating__star">
              <span className="gmaps-rating__star-empty">★</span>
              <span
                className="gmaps-rating__star-fill"
                style={{ width: `${fill * 100}%` }}
              >
                ★
              </span>
            </span>
          );
        })}
      </span>
      <span className="gmaps-rating__count">
        {showReviewsLabel
          ? t("places.reviewCount", { count: countLabel })
          : `(${countLabel})`}
      </span>
    </div>
  );
}
